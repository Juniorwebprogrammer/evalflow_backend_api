using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.AiAnalysis.AiAnalysisDto;
using evalflow_backend_api.Features.EvaluationComparisons.EvaluationComparisonsDto;
using evalflow_backend_api.Features.EvaluationComparisons.GetCycleComparisons;
using evalflow_backend_api.Infrastructure.AI;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Plans;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Features.AiAnalysis.RequestAiAnalysis;

public class RequestAiAnalysisHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IPlanLimitService planLimits,
    IEncryptionService encryptionService,
    ISender sender,
    AiAnalysisQueueSignal queueSignal,
    IOptions<AiSettings> aiSettings,
    ILogger<RequestAiAnalysisHandler> logger
) : IRequestHandler<RequestAiAnalysisRecord, IResult>
{
    public const string FeatureNotAvailableCode = "PLAN_FEATURE_NOT_AVAILABLE";

    public async Task<IResult> Handle(RequestAiAnalysisRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();
        if (!int.TryParse(currentUser.GetUserId(), out var userId)) return Results.Unauthorized();

        var companyId = await dbContext.Companies
            .Where(c => c.IdentificationId == tenantId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (companyId is null) return Results.Unauthorized();

        if (!await planLimits.HasFeatureAsync(companyId.Value, PlanFeature.Ai, cancellationToken))
        {
            return Results.Json(
                new PlanFeatureError(FeatureNotAvailableCode,
                    "El análisis con IA está disponible en los planes Growth y Enterprise. Mejora tu plan para usarlo.", "Ai"),
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!aiSettings.Value.Enabled)
        {
            return Results.Json(new { Message = "El análisis con IA no está disponible en este momento." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var cycle = await dbContext.EvaluationCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CycleId && c.EmpresaID == companyId.Value, cancellationToken);
        if (cycle is null) return Results.NotFound(new { Message = "Ciclo no encontrado." });

        var comparisonResult = await sender.Send(new GetCycleComparisonsRecord(cycle.Id, request.EvaluatedUserId), cancellationToken);
        if (comparisonResult is not Ok<CycleComparisonsDto> ok) return comparisonResult;

        // Only employees whose evaluation(s) are complete have something to analyse.
        var targets = ok.Value!.Comparisons
            .Where(c => c.Summary is not null && c.Questions.Count > 0)
            .Where(c => request.TemplateId is null || c.TemplateId == request.TemplateId)
            .ToList();
        if (targets.Count == 0)
            return Results.BadRequest(new { Message = "No hay evaluaciones completadas que analizar en este ciclo." });

        var clarifications = await FetchClarificationsAsync(cycle.Id, request.EvaluatedUserId, cancellationToken);
        var latest = await FetchLatestAnalysesAsync(cycle.Id, request.EvaluatedUserId, cancellationToken);

        var reused = new List<AiEvaluationAnalysis>();
        var created = new List<AiEvaluationAnalysis>();

        foreach (var target in targets)
        {
            var context = AiAnalysisContextBuilder.Build(cycle.TipoEvaluación, target,
                clarifications.Where(c => c.TemplateId == target.TemplateId && c.EvaluatedUserId == target.EvaluatedUserId)
                    .Select(c => c.Clarification));
            var serialized = AiAnalysisContextBuilder.Serialize(context);
            var hash = AiAnalysisContextBuilder.Hash(serialized);

            // Same data as a pending/done analysis: nothing new to say, and it doesn't cost quota.
            if (latest.TryGetValue((target.TemplateId, target.EvaluatedUserId), out var previous)
                && previous.InputHash == hash
                && previous.Estado != AiAnalysisStatus.Error
                && (!request.Force || previous.Estado != AiAnalysisStatus.Completado))
            {
                reused.Add(previous);
                continue;
            }

            created.Add(new AiEvaluationAnalysis
            {
                EmpresaID = companyId.Value,
                EvaluationCycleId = cycle.Id,
                TemplateId = target.TemplateId,
                EvaluatedUserId = target.EvaluatedUserId,
                RequestedByUserId = userId,
                InputHash = hash,
                InputEncrypted = encryptionService.Encrypt(serialized),
            });
        }

        if (created.Count > 0)
        {
            var limitError = await planLimits.RunExclusiveAsync(companyId.Value, async ct =>
            {
                var error = await planLimits.CheckAsync(companyId.Value, PlanLimit.AiAnalysesPerMonth, ct, adding: created.Count);
                if (error is not null) return error;

                dbContext.AiEvaluationAnalyses.AddRange(created);
                await dbContext.SaveChangesAsync(ct);
                return null;
            }, cancellationToken);
            if (limitError is not null) return limitError;

            queueSignal.Notify();
            logger.LogInformation("AiAnalysis: {Created} análisis encolados para el ciclo {CycleId} ({Reused} reutilizados).",
                created.Count, cycle.Id, reused.Count);
        }

        var analyses = created.Concat(reused)
            .Select(a => AiAnalysisMapper.ToDto(a, encryptionService, logger))
            .ToList();

        return Results.Accepted(value: new RequestAiAnalysisResponse(created.Count, reused.Count, analyses));
    }

    private async Task<Dictionary<(int TemplateId, int EvaluatedUserId), AiEvaluationAnalysis>> FetchLatestAnalysesAsync(
        int cycleId, int? evaluatedUserId, CancellationToken cancellationToken)
    {
        var query = dbContext.AiEvaluationAnalyses.Where(a => a.EvaluationCycleId == cycleId);
        if (evaluatedUserId.HasValue) query = query.Where(a => a.EvaluatedUserId == evaluatedUserId.Value);

        var analyses = await query.AsNoTracking().ToListAsync(cancellationToken);

        return analyses
            .GroupBy(a => (a.TemplateId, a.EvaluatedUserId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.FechaCreacion).ThenByDescending(a => a.Id).First());
    }

    private async Task<List<(int TemplateId, int EvaluatedUserId, ClarificationForAnalysis Clarification)>> FetchClarificationsAsync(
        int cycleId, int? evaluatedUserId, CancellationToken cancellationToken)
    {
        var query = dbContext.ClarificationRequests.AsNoTracking().Where(c => c.EvaluationCycleId == cycleId);
        if (evaluatedUserId.HasValue) query = query.Where(c => c.EvaluatedUserId == evaluatedUserId.Value);

        var clarifications = await query.OrderBy(c => c.FechaCreacion).ToListAsync(cancellationToken);

        return clarifications
            .Select(c => (c.TemplateId, c.EvaluatedUserId, new ClarificationForAnalysis(
                c.QuestionId,
                c.Mensaje,
                Decrypt(c.EvaluatedResponseEncrypted, c.Id),
                Decrypt(c.ManagerResponseEncrypted, c.Id),
                c.Estado)))
            .ToList();
    }

    private string? Decrypt(string? cipher, int clarificationId)
    {
        if (string.IsNullOrEmpty(cipher)) return null;

        try
        {
            return encryptionService.Decrypt(cipher);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AiAnalysis: no se pudo descifrar la respuesta de la solicitud {ClarificationId}.", clarificationId);
            return null;
        }
    }
}
