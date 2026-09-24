using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Frontend;
using evalflow_backend_api.Infrastructure.Notifications; // Tu servicio de correos
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GenerateSubmissions;

public class GenerateSubmissionsHandler(
    AppDbContext dbContext, 
    ICurrentUserService currentUser,
    IEmailService emailService,
    IOptions<FrontendSettings> frontendSettings) : IRequestHandler<GenerateSubmissionsRecord, IResult>
{
    public async Task<IResult> Handle(GenerateSubmissionsRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var cycle = await GetCycleWithDetailsAsync(request.CycleId, tenantId, cancellationToken);
        if (cycle is null) return Results.NotFound(new { Message = "Ciclo no encontrado." });

        var newSubmissions = await GenerateMissingSubmissionsAsync(cycle, cancellationToken);
        
        if (newSubmissions.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await SendNotificationEmailsAsync(newSubmissions, cycle.Nombre, cancellationToken);
        }

        return Results.Ok(new { Message = $"Se han generado {newSubmissions.Count} formularios nuevos con éxito." });
    }

    private async Task<EvaluationCycle?> GetCycleWithDetailsAsync(int cycleId, string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationCycles
            .Include(c => c.Templates).ThenInclude(t => t.UsuariosAsignados)
            .Include(c => c.Templates).ThenInclude(t => t.Preguntas)
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.Empresa!.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<List<EvaluationSubmission>> GenerateMissingSubmissionsAsync(EvaluationCycle cycle, CancellationToken cancellationToken)
    {
        var newSubmissions = new List<EvaluationSubmission>();
        var existingSubmissions = await GetExistingSubmissionsAsync(cycle.Id, cancellationToken);

        bool incluyeAutoevaluacion = cycle.TipoEvaluación == EvaluationType.Auto || cycle.TipoEvaluación == EvaluationType.Evaluacion360;
        bool incluyeEvaluador = cycle.TipoEvaluación == EvaluationType.Evaluacion180 || cycle.TipoEvaluación == EvaluationType.Evaluacion360;
        
        foreach (var template in cycle.Templates)
        {
            foreach (var user in template.UsuariosAsignados)
            {
                if (incluyeAutoevaluacion)
                {
                    if (TryCreateSubmission(cycle.Id, template.Id, user.Id, user.Id, existingSubmissions, out var selfSubmission, template.Preguntas))
                        newSubmissions.Add(selfSubmission);
                }

                if (incluyeEvaluador && user.SuperiorId.HasValue)
                {
                    if (TryCreateSubmission(cycle.Id, template.Id, user.Id, user.SuperiorId.Value, existingSubmissions, out var managerSubmission, template.Preguntas))
                        newSubmissions.Add(managerSubmission);
                }
            }
        }

        if (newSubmissions.Count > 0) dbContext.EvaluationSubmissions.AddRange(newSubmissions);
        return newSubmissions;
    }

    private async Task<HashSet<string>> GetExistingSubmissionsAsync(int cycleId, CancellationToken cancellationToken)
    {
        var submissions = await dbContext.EvaluationSubmissions
            .Where(s => s.EvaluationCycleId == cycleId)
            .Select(s => $"{s.TemplateId}-{s.EvaluatedUserId}-{s.RespondentUserId}")
            .ToListAsync(cancellationToken);
        return [.. submissions];
    }

    private bool TryCreateSubmission(int cycleId, int templateId, int evaluatedId, int respondentId, HashSet<string> existing, out EvaluationSubmission newSubmission, IEnumerable<Question> questions)
    {
        var signature = $"{templateId}-{evaluatedId}-{respondentId}";
        if (existing.Contains(signature))
        {
            newSubmission = null!;
            return false;
        }

        newSubmission = new EvaluationSubmission
        {
            EvaluationCycleId = cycleId, TemplateId = templateId, EvaluatedUserId = evaluatedId, RespondentUserId = respondentId, IsCompleted = false, ReminderSent = false,
            Answers = questions.Select(q => new Answer { QuestionId = q.Id, EncryptedPayload = string.Empty }).ToList()
        };

        existing.Add(signature);
        return true;
    }

    private async Task SendNotificationEmailsAsync(List<EvaluationSubmission> newSubmissions, string cycleName, CancellationToken cancellationToken)
    {
        // Agrupamos por el usuario que tiene que responder
        var respondentIds = newSubmissions.Select(s => s.RespondentUserId).Distinct().ToList();

        var respondents = await dbContext.Users
            .Where(u => respondentIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        foreach (var respondent in respondents)
        {
            var pendingCount = newSubmissions.Count(s => s.RespondentUserId == respondent.Id);
            var subject = "Nuevas evaluaciones asignadas - EvalFlow";
            var body = await GenerateEvaluationAssignedEmailHtmlAsync(respondent.Nombre, cycleName, pendingCount);

            // Usamos fire-and-forget o await dependiendo de si quieres que la API espere
            await emailService.SendEmailAsync(respondent.Email, subject, body, cancellationToken);
        }
    }

    private async Task<string> GenerateEvaluationAssignedEmailHtmlAsync(string userNombre, string cycleName, int pendingCount)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Notifications", "Templates", "EvaluationAssignedEmail.html");
        var template = await File.ReadAllTextAsync(templatePath);
        var dashboardLink = frontendSettings.Value.BuildLink("dashboard/mis-evaluaciones");
        var currentYear = DateTime.UtcNow.Year.ToString();
        var formWord = pendingCount == 1 ? "formulario" : "formularios";

        return template
            .Replace("{{UserNombre}}", userNombre)
            .Replace("{{CycleName}}", cycleName)
            .Replace("{{PendingCount}}", pendingCount.ToString())
            .Replace("{{FormWord}}", formWord)
            .Replace("{{DashboardLink}}", dashboardLink)
            .Replace("{{Year}}", currentYear);
    }
}