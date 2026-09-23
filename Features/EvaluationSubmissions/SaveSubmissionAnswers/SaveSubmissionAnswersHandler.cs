using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.EvaluationSubmissions.EvaluationSubmissionsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using evalflow_backend_api.Infrastructure.SignalR;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationSubmissions.SaveSubmissionAnswers;

public class SaveSubmissionAnswersHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService,
    IHubContext<DashboardHub> hubContext,
    ILogger<SaveSubmissionAnswersHandler> logger
) : IRequestHandler<SaveSubmissionAnswersRecord, IResult>
{
    public async Task<IResult> Handle(SaveSubmissionAnswersRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        var userId = GetUserId();
        if (userId is null) return Results.Unauthorized();

        var submission = await GetSubmissionWithAnswersAsync(request.SubmissionId, userId.Value, cancellationToken);
        if (submission is null) return Results.NotFound(new { Message = "Formulario no encontrado o no tienes permiso." });

        if (submission.IsCompleted) return Results.BadRequest(new { Message = "Este formulario ya ha sido enviado." });

        if (submission.Cycle!.FechaFin < DateTime.UtcNow)
        {
            return Results.BadRequest(new { Message = "El plazo para completar esta evaluación ha finalizado." });
        }
        
        var validationError = ValidateAnswers(submission, request.Answers);
        if (validationError is not null) return validationError;

        await EncryptAndSaveAnswersAsync(submission, request.Answers, cancellationToken);

        await NotifyDashboardAsync(tenantId, submission, cancellationToken);

        return Results.Ok(new { Message = "Respuestas guardadas y formulario completado con éxito." });
    }

    private int? GetUserId()
    {
        var userIdStr = currentUser.GetUserId();
        return int.TryParse(userIdStr, out var userId) ? userId : null;
    }

    private async Task<EvaluationSubmission?> GetSubmissionWithAnswersAsync(int submissionId, int respondentUserId, CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationSubmissions
            .Include(s => s.Answers)
            .Include(s => s.Cycle)
            .Include(s => s.EvaluatedUser)
            .Include(s => s.RespondentUser)
            .Include(s => s.Template)
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.RespondentUserId == respondentUserId, cancellationToken);
    }

    private IResult? ValidateAnswers(EvaluationSubmission submission, List<AnswerInputDto> providedAnswers)
    {
        if (providedAnswers.Count != submission.Answers.Count)
            return Results.BadRequest(new { Message = "Faltan respuestas. Debes contestar todas las preguntas del formulario." });

        if (providedAnswers.Any(a => string.IsNullOrWhiteSpace(a.RawPayload)))
            return Results.BadRequest(new { Message = "Ninguna respuesta puede estar vacía." });

        return null;
    }

    private async Task NotifyDashboardAsync(string? tenantId, EvaluationSubmission submission,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            logger.LogWarning(
                "SaveSubmissionAnswers: submission {SubmissionId} completada pero sin tenantId — no se notificó al dashboard.",
                submission.Id);
            return;
        }

        var notification = new EvaluationCompletedNotification(
            submission.Id,
            submission.EvaluationCycleId,
            submission.RespondentUserId,
            $"{submission.RespondentUser!.Nombre} {submission.RespondentUser.Apellidos}",
            submission.EvaluatedUserId,
            $"{submission.EvaluatedUser!.Nombre} {submission.EvaluatedUser.Apellidos}",
            submission.Template!.Titulo,
            submission.RespondentUserId == submission.EvaluatedUserId,
            DateTime.UtcNow
        );

        logger.LogInformation(
            "SaveSubmissionAnswers: notificando al grupo del tenant {TenantId} — submission {SubmissionId} del ciclo {CycleId} completada.",
            tenantId, submission.Id, submission.EvaluationCycleId);

        await hubContext.Clients.Group(tenantId).SendAsync("EvaluationCompleted", notification, cancellationToken);
    }
    
    private async Task EncryptAndSaveAnswersAsync(EvaluationSubmission submission, List<AnswerInputDto> providedAnswers, CancellationToken cancellationToken)
    {
        foreach (var answerDto in providedAnswers)
        {
            var targetAnswer = submission.Answers.FirstOrDefault(a => a.QuestionId == answerDto.QuestionId);
            if (targetAnswer is not null)
            {
                targetAnswer.EncryptedPayload = encryptionService.Encrypt(answerDto.RawPayload);
            }
        }

        submission.IsCompleted = true;
        submission.SubmittedAt = DateTime.UtcNow;

        dbContext.EvaluationSubmissions.Update(submission);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}