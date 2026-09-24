using System.Net;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.Clarifications.ClarificationsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Frontend;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Features.Clarifications.CreateClarification;

public class CreateClarificationHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IEmailService emailService,
    IOptions<FrontendSettings> frontendSettings,
    ILogger<CreateClarificationHandler> logger
) : IRequestHandler<CreateClarificationRecord, IResult>
{
    private const int MaxMensajeLength = 1000;

    public async Task<IResult> Handle(CreateClarificationRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        if (!int.TryParse(currentUser.GetUserId(), out var requesterId)) return Results.Unauthorized();

        var validationError = ValidateMensaje(request.Mensaje);
        if (validationError is not null) return validationError;

        var cycle = await GetCycleAsync(request.CycleId, tenantId, cancellationToken);
        if (cycle is null) return Results.NotFound(new { Message = "Ciclo no encontrado." });

        if (cycle.TipoEvaluación != EvaluationType.Evaluacion360)
        {
            return Results.BadRequest(new
            {
                Message = "Solo se puede solicitar más información en ciclos 360 (autoevaluación + evaluación del superior)."
            });
        }

        var submissions = await FetchSubmissionsAsync(request, cancellationToken);

        var self = submissions.FirstOrDefault(s => s.RespondentUserId == s.EvaluatedUserId);
        var manager = FindManagerSubmission(submissions);

        if (self?.IsCompleted != true || manager?.IsCompleted != true)
        {
            return Results.BadRequest(new
            {
                Message = "La autoevaluación y la evaluación del superior deben estar completadas para solicitar más información."
            });
        }

        var question = await GetQuestionAsync(request.QuestionId, request.TemplateId, cancellationToken);
        if (request.QuestionId.HasValue && question is null)
            return Results.BadRequest(new { Message = "La pregunta indicada no pertenece a la plantilla." });

        var requester = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == requesterId, cancellationToken);
        if (requester is null) return Results.Unauthorized();

        var clarification = CreateClarification(request, manager.RespondentUserId, requesterId);
        await SaveToDatabaseAsync(clarification, cancellationToken);

        await NotifyParticipantsAsync(cycle, self, manager, question, clarification.Mensaje, cancellationToken);

        return Results.Created($"/clarifications/{clarification.Id}", MapClarification(clarification, self, manager, question, requester));
    }

    private static IResult? ValidateMensaje(string? mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
            return Results.BadRequest(new { Message = "Indica qué información necesitas." });

        if (mensaje.Trim().Length > MaxMensajeLength)
            return Results.BadRequest(new { Message = $"El mensaje no puede superar los {MaxMensajeLength} caracteres." });

        return null;
    }

    private async Task<EvaluationCycle?> GetCycleAsync(int cycleId, string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.Empresa!.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<List<EvaluationSubmission>> FetchSubmissionsAsync(CreateClarificationRecord request, CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationSubmissions
            .AsNoTracking()
            .Where(s => s.EvaluationCycleId == request.CycleId
                        && s.TemplateId == request.TemplateId
                        && s.EvaluatedUserId == request.EvaluatedUserId)
            .Include(s => s.EvaluatedUser)
            .Include(s => s.RespondentUser)
            .Include(s => s.Template)
            .ToListAsync(cancellationToken);
    }

    private static EvaluationSubmission? FindManagerSubmission(List<EvaluationSubmission> submissions)
    {
        return submissions
            .Where(s => s.RespondentUserId != s.EvaluatedUserId)
            .OrderByDescending(s => s.IsCompleted)
            .ThenByDescending(s => s.SubmittedAt)
            .FirstOrDefault();
    }

    private async Task<Question?> GetQuestionAsync(int? questionId, int templateId, CancellationToken cancellationToken)
    {
        if (!questionId.HasValue) return null;

        return await dbContext.Questions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == questionId.Value && q.TemplateId == templateId, cancellationToken);
    }

    private static ClarificationRequest CreateClarification(CreateClarificationRecord request, int managerUserId, int requesterId)
    {
        return new ClarificationRequest
        {
            EvaluationCycleId = request.CycleId,
            TemplateId = request.TemplateId,
            QuestionId = request.QuestionId,
            EvaluatedUserId = request.EvaluatedUserId,
            ManagerUserId = managerUserId,
            RequestedByUserId = requesterId,
            Mensaje = request.Mensaje.Trim(),
            Estado = ClarificationStatus.Pendiente,
            FechaCreacion = DateTime.UtcNow
        };
    }

    private async Task SaveToDatabaseAsync(ClarificationRequest clarification, CancellationToken cancellationToken)
    {
        dbContext.ClarificationRequests.Add(clarification);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task NotifyParticipantsAsync(EvaluationCycle cycle, EvaluationSubmission self, EvaluationSubmission manager,
        Question? question, string mensaje, CancellationToken cancellationToken)
    {
        var evaluated = self.EvaluatedUser!;
        var evaluator = manager.RespondentUser!;
        var templateTitle = self.Template!.Titulo;

        await SendEmailAsync(evaluated,
            "tu autoevaluación",
            cycle.Nombre, templateTitle, question, mensaje, cancellationToken);

        await SendEmailAsync(evaluator,
            $"tu evaluación de {evaluated.Nombre} {evaluated.Apellidos}",
            cycle.Nombre, templateTitle, question, mensaje, cancellationToken);
    }

    private async Task SendEmailAsync(User recipient, string evaluationLabel, string cycleName, string templateTitle,
        Question? question, string mensaje, CancellationToken cancellationToken)
    {
        try
        {
            var body = await BuildEmailBodyAsync(recipient.Nombre, evaluationLabel, cycleName, templateTitle, question, mensaje, cancellationToken);

            await emailService.SendEmailAsync(recipient.Email, "Te han solicitado más información sobre una evaluación - EvalFlow", body, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CreateClarification: no se pudo enviar el email de solicitud de información al usuario {UserId}.", recipient.Id);
        }
    }

    private async Task<string> BuildEmailBodyAsync(string userNombre, string evaluationLabel, string cycleName, string templateTitle,
        Question? question, string mensaje, CancellationToken cancellationToken)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Notifications", "Templates", "ClarificationRequestedEmail.html");
        var link = frontendSettings.Value.BuildLink("dashboard/solicitudes-informacion");
        var questionText = question is null ? "Evaluación completa" : question.Texto;

        if (!File.Exists(templatePath))
        {
            return $"<p>Hola {Encode(userNombre)},</p><p>RRHH necesita más información sobre {Encode(evaluationLabel)} en el ciclo <b>{Encode(cycleName)}</b>.</p><p>{Encode(mensaje)}</p><p>Puedes responder <a href='{link}'>aquí</a>.</p>";
        }

        var template = await File.ReadAllTextAsync(templatePath, cancellationToken);

        return template
            .Replace("{{UserNombre}}", Encode(userNombre))
            .Replace("{{EvaluationLabel}}", Encode(evaluationLabel))
            .Replace("{{CycleName}}", Encode(cycleName))
            .Replace("{{TemplateTitle}}", Encode(templateTitle))
            .Replace("{{QuestionText}}", Encode(questionText))
            .Replace("{{Mensaje}}", Encode(mensaje))
            .Replace("{{Link}}", link)
            .Replace("{{Year}}", DateTime.UtcNow.Year.ToString());
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static ClarificationDto MapClarification(ClarificationRequest clarification, EvaluationSubmission self,
        EvaluationSubmission manager, Question? question, User requester)
    {
        return new ClarificationDto(
            clarification.Id,
            clarification.EvaluationCycleId,
            clarification.TemplateId,
            self.Template!.Titulo,
            clarification.QuestionId,
            question?.Texto,
            clarification.EvaluatedUserId,
            $"{self.EvaluatedUser!.Nombre} {self.EvaluatedUser.Apellidos}",
            clarification.ManagerUserId,
            $"{manager.RespondentUser!.Nombre} {manager.RespondentUser.Apellidos}",
            $"{requester.Nombre} {requester.Apellidos}",
            clarification.Mensaje,
            null,
            null,
            null,
            null,
            clarification.Estado,
            clarification.FechaCreacion
        );
    }
}
