using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationComparisons.AcceptDiscrepancies;

public class AcceptDiscrepanciesHandler(AppDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<AcceptDiscrepanciesRecord, IResult>
{
    public async Task<IResult> Handle(AcceptDiscrepanciesRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        if (!int.TryParse(currentUser.GetUserId(), out var userId)) return Results.Unauthorized();

        var questionIds = request.QuestionIds.Distinct().ToList();
        if (questionIds.Count == 0)
            return Results.BadRequest(new { Message = "Indica al menos una pregunta a aceptar." });

        if (!Enum.IsDefined(request.Source))
            return Results.BadRequest(new { Message = "La respuesta aceptada no es válida." });

        var cycle = await dbContext.EvaluationCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CycleId && c.Empresa!.IdentificationId == tenantId, cancellationToken);
        if (cycle is null) return Results.NotFound(new { Message = "Ciclo no encontrado." });

        if (cycle.TipoEvaluación != EvaluationType.Evaluacion360)
            return Results.BadRequest(new { Message = "Solo se pueden aceptar desequilibrios en ciclos 360." });

        if (cycle.FechaCompletado.HasValue)
            return Results.Conflict(new { Message = "El ciclo ya se ha completado y no admite cambios." });

        if (!await BothEvaluationsCompletedAsync(request, cancellationToken))
        {
            return Results.BadRequest(new
            {
                Message = "La autoevaluación y la evaluación del superior deben estar completadas para aceptar un desequilibrio."
            });
        }

        if (!await QuestionsBelongToTemplateAsync(questionIds, request.TemplateId, cancellationToken))
            return Results.BadRequest(new { Message = "Alguna de las preguntas no pertenece a la plantilla." });

        var acceptances = await UpsertAcceptancesAsync(request, questionIds, userId, cancellationToken);

        return Results.Ok(acceptances
            .OrderBy(a => a.QuestionId)
            .Select(a => new AcceptedDiscrepancyDto(a.QuestionId, a.AcceptedSource, a.AcceptedAt))
            .ToList());
    }

    private async Task<bool> BothEvaluationsCompletedAsync(AcceptDiscrepanciesRecord request, CancellationToken cancellationToken)
    {
        var submissions = await dbContext.EvaluationSubmissions
            .AsNoTracking()
            .Where(s => s.EvaluationCycleId == request.CycleId
                        && s.TemplateId == request.TemplateId
                        && s.EvaluatedUserId == request.EvaluatedUserId
                        && s.IsCompleted)
            .Select(s => new { s.RespondentUserId, s.EvaluatedUserId })
            .ToListAsync(cancellationToken);

        return submissions.Any(s => s.RespondentUserId == s.EvaluatedUserId)
               && submissions.Any(s => s.RespondentUserId != s.EvaluatedUserId);
    }

    private async Task<bool> QuestionsBelongToTemplateAsync(List<int> questionIds, int templateId, CancellationToken cancellationToken)
    {
        var matches = await dbContext.Questions
            .CountAsync(q => q.TemplateId == templateId && questionIds.Contains(q.Id), cancellationToken);

        return matches == questionIds.Count;
    }

    private async Task<List<DiscrepancyAcceptance>> UpsertAcceptancesAsync(AcceptDiscrepanciesRecord request, List<int> questionIds,
        int userId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.DiscrepancyAcceptances
            .Where(a => a.EvaluationCycleId == request.CycleId
                        && a.TemplateId == request.TemplateId
                        && a.EvaluatedUserId == request.EvaluatedUserId
                        && questionIds.Contains(a.QuestionId))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var result = new List<DiscrepancyAcceptance>();

        foreach (var questionId in questionIds)
        {
            var acceptance = existing.FirstOrDefault(a => a.QuestionId == questionId);
            if (acceptance is null)
            {
                acceptance = new DiscrepancyAcceptance
                {
                    EvaluationCycleId = request.CycleId,
                    TemplateId = request.TemplateId,
                    EvaluatedUserId = request.EvaluatedUserId,
                    QuestionId = questionId
                };
                dbContext.DiscrepancyAcceptances.Add(acceptance);
            }

            acceptance.AcceptedSource = request.Source;
            acceptance.AcceptedByUserId = userId;
            acceptance.AcceptedAt = now;
            result.Add(acceptance);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }
}
