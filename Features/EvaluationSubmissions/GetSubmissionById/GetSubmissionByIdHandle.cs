using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.EvaluationSubmissions;
using evalflow_backend_api.Features.EvaluationSubmissions.EvaluationSubmissionsDto; // Para los DTOs
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GetSubmissionById;

public class GetSubmissionByIdHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetSubmissionByIdRecord, IResult>
{
    public async Task<IResult> Handle(GetSubmissionByIdRecord request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Results.Unauthorized();

        var submission = await FetchSubmissionDetailsAsync(request.SubmissionId, userId.Value, cancellationToken);
        if (submission is null) return Results.NotFound(new { Message = "Formulario no encontrado o no tienes permiso para verlo." });

        var responseDto = MapToDto(submission);

        return Results.Ok(responseDto);
    }

    private int? GetUserId()
    {
        var userIdStr = currentUser.GetUserId();
        return int.TryParse(userIdStr, out var userId) ? userId : null;
    }

    private async Task<EvaluationSubmission?> FetchSubmissionDetailsAsync(int submissionId, int respondentUserId, CancellationToken cancellationToken)
    {
        // Traemos la entrega con toda la jerarquía necesaria para pintar el formulario
        return await dbContext.EvaluationSubmissions
            .AsNoTracking()
            .Include(s => s.Cycle)
            .Include(s => s.EvaluatedUser)
            .Include(s => s.Template)
                .ThenInclude(t => t!.Preguntas)
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.RespondentUserId == respondentUserId, cancellationToken);
    }

    private SubmissionDetailDto MapToDto(EvaluationSubmission submission)
    {
        // Mapeamos las preguntas y las ordenamos según el campo 'Orden'
        var questionsDto = submission.Template!.Preguntas
            .OrderBy(q => q.Orden)
            .Select(q => new SubmissionQuestionDto(
                q.Id,
                q.Texto,
                q.Tipo,
                q.Opciones,
                q.Orden
            )).ToList();

        return new SubmissionDetailDto(
            submission.Id,
            submission.IsCompleted,
            submission.Cycle!.Nombre,
            $"{submission.EvaluatedUser!.Nombre} {submission.EvaluatedUser.Apellidos}",
            submission.Template.Titulo,
            submission.Template.Descripcion,
            questionsDto,
            submission.Cycle.FechaFin
        );
    }
}