using System.Net;
using System.Text.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationComparisons.EvaluationComparisonsDto;
using evalflow_backend_api.Features.EvaluationComparisons.GetCycleComparisons;
using evalflow_backend_api.Features.EvaluationResults.EvaluationResultsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Frontend;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Features.EvaluationCycles.CompleteEvaluationCycle;

public class CompleteEvaluationCycleHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService,
    ISender sender,
    IEmailService emailService,
    IOptions<FrontendSettings> frontendSettings,
    ILogger<CompleteEvaluationCycleHandler> logger
) : IRequestHandler<CompleteEvaluationCycleRecord, IResult>
{
    public async Task<IResult> Handle(CompleteEvaluationCycleRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        if (!int.TryParse(currentUser.GetUserId(), out var userId)) return Results.Unauthorized();

        var cycle = await dbContext.EvaluationCycles
            .Include(c => c.Empresa)
            .FirstOrDefaultAsync(c => c.Id == request.CycleId && c.Empresa!.IdentificationId == tenantId, cancellationToken);
        if (cycle is null) return Results.NotFound(new { Message = "Ciclo no encontrado." });

        if (cycle.FechaCompletado.HasValue)
            return Results.Conflict(new { Message = "El ciclo ya se ha completado." });

        var comparisons = new Dictionary<(int EvaluatedUserId, int TemplateId), EmployeeComparisonDto>();
        if (cycle.TipoEvaluación == EvaluationType.Evaluacion360)
        {
            var comparisonResult = await sender.Send(new GetCycleComparisonsRecord(cycle.Id, null), cancellationToken);
            if (comparisonResult is not Ok<CycleComparisonsDto> ok) return comparisonResult;

            if (ok.Value!.PendingImbalances > 0)
            {
                return Results.Conflict(new
                {
                    Message = $"Quedan {ok.Value.PendingImbalances} desequilibrios sin aceptar. Acéptalos antes de completar la evaluación.",
                    ok.Value.PendingImbalances
                });
            }

            comparisons = ok.Value.Comparisons.ToDictionary(c => (c.EvaluatedUserId, c.TemplateId));
        }

        var submissions = await FetchSubmissionsAsync(cycle.Id, cancellationToken);
        if (submissions.Count == 0)
            return Results.BadRequest(new { Message = "El ciclo no tiene formularios generados." });

        var acceptances = await FetchAcceptancesAsync(cycle.Id, cancellationToken);

        var completedAt = DateTime.UtcNow;
        var autoCompletedIds = AutoCompletePendingSubmissions(submissions, completedAt);

        var results = submissions
            .GroupBy(s => new { s.EvaluatedUserId, s.TemplateId })
            .Select(group => BuildResult(cycle, group.ToList(), comparisons, acceptances, autoCompletedIds, userId, completedAt))
            .ToList();

        cycle.FechaCompletado = completedAt;
        cycle.Activo = false;
        dbContext.EvaluationResults.AddRange(results);
        await dbContext.SaveChangesAsync(cancellationToken);

        await NotifyEmployeesAsync(submissions, cycle.Nombre, cancellationToken);

        return Results.Ok(new CompleteEvaluationCycleResponse(
            "Evaluación completada y resultados generados con éxito.",
            results.Count,
            autoCompletedIds.Count,
            completedAt));
    }

    private async Task<List<EvaluationSubmission>> FetchSubmissionsAsync(int cycleId, CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationSubmissions
            .Where(s => s.EvaluationCycleId == cycleId)
            .Include(s => s.Answers)
            .Include(s => s.EvaluatedUser).ThenInclude(u => u!.Cargo)
            .Include(s => s.EvaluatedUser).ThenInclude(u => u!.Departamento)
            .Include(s => s.RespondentUser)
            .Include(s => s.Template).ThenInclude(t => t!.Preguntas)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<(int TemplateId, int EvaluatedUserId, int QuestionId), AcceptedAnswerSource>> FetchAcceptancesAsync(
        int cycleId, CancellationToken cancellationToken)
    {
        var acceptances = await dbContext.DiscrepancyAcceptances
            .AsNoTracking()
            .Where(a => a.EvaluationCycleId == cycleId)
            .ToListAsync(cancellationToken);

        return acceptances.ToDictionary(a => (a.TemplateId, a.EvaluatedUserId, a.QuestionId), a => a.AcceptedSource);
    }

    private static HashSet<int> AutoCompletePendingSubmissions(List<EvaluationSubmission> submissions, DateTime completedAt)
    {
        var autoCompleted = new HashSet<int>();

        foreach (var submission in submissions.Where(s => !s.IsCompleted))
        {
            submission.IsCompleted = true;
            submission.SubmittedAt = completedAt;
            autoCompleted.Add(submission.Id);
        }

        return autoCompleted;
    }

    private EvaluationResult BuildResult(EvaluationCycle cycle, List<EvaluationSubmission> submissions,
        Dictionary<(int EvaluatedUserId, int TemplateId), EmployeeComparisonDto> comparisons,
        Dictionary<(int TemplateId, int EvaluatedUserId, int QuestionId), AcceptedAnswerSource> acceptances,
        HashSet<int> autoCompletedIds, int completedByUserId, DateTime completedAt)
    {
        var reference = submissions[0];
        var evaluated = reference.EvaluatedUser!;
        var template = reference.Template!;
        var self = submissions.FirstOrDefault(s => s.RespondentUserId == s.EvaluatedUserId);
        var manager = submissions
            .Where(s => s.RespondentUserId != s.EvaluatedUserId)
            .OrderBy(s => autoCompletedIds.Contains(s.Id))
            .ThenByDescending(s => s.SubmittedAt)
            .FirstOrDefault();

        comparisons.TryGetValue((reference.EvaluatedUserId, reference.TemplateId), out var comparison);
        var levels = comparison?.Questions.ToDictionary(q => q.QuestionId, q => q.Level) ?? [];

        var questions = template.Preguntas
            .OrderBy(q => q.Orden)
            .Select(question => BuildQuestion(question, self, manager, levels, acceptances, reference))
            .ToList();

        var snapshot = new EvaluationResultSnapshot(
            cycle.Empresa!.Nombre,
            cycle.Nombre,
            cycle.TipoEvaluación,
            cycle.FechaInicio,
            cycle.FechaFin,
            template.Titulo,
            template.Descripcion,
            $"{evaluated.Nombre} {evaluated.Apellidos}",
            evaluated.Email,
            evaluated.Cargo?.Nombre,
            evaluated.Departamento?.Nombre,
            manager is null ? null : $"{manager.RespondentUser!.Nombre} {manager.RespondentUser.Apellidos}",
            self is not null && !autoCompletedIds.Contains(self.Id),
            manager is not null && !autoCompletedIds.Contains(manager.Id),
            submissions.Any(s => autoCompletedIds.Contains(s.Id)),
            completedAt,
            AverageOrNull(questions.Where(q => q.Tipo != QuestionType.Seleccion).Select(q => ParseNumber(q.SelfAnswer))),
            AverageOrNull(questions.Where(q => q.Tipo != QuestionType.Seleccion).Select(q => ParseNumber(q.ManagerAnswer))),
            AverageOrNull(questions.Where(q => q.Tipo != QuestionType.Seleccion).Select(q => ParseNumber(q.FinalAnswer))),
            comparison?.Summary?.AlignmentPercentage,
            questions
        );

        return new EvaluationResult
        {
            EvaluationCycleId = cycle.Id,
            TemplateId = template.Id,
            EvaluatedUserId = evaluated.Id,
            CompletedByUserId = completedByUserId,
            CompletedAt = completedAt,
            AverageFinal = snapshot.AverageFinal,
            EncryptedSnapshot = encryptionService.Encrypt(JsonSerializer.Serialize(snapshot))
        };
    }

    private ResultQuestionSnapshot BuildQuestion(Question question, EvaluationSubmission? self, EvaluationSubmission? manager,
        Dictionary<int, AlignmentLevel> levels,
        Dictionary<(int TemplateId, int EvaluatedUserId, int QuestionId), AcceptedAnswerSource> acceptances,
        EvaluationSubmission reference)
    {
        var selfAnswer = FormatAnswer(question, self is null ? null : DecryptAnswer(self, question.Id));
        var managerAnswer = FormatAnswer(question, manager is null ? null : DecryptAnswer(manager, question.Id));

        var accepted = acceptances.TryGetValue((reference.TemplateId, reference.EvaluatedUserId, question.Id), out var acceptedSource);
        AcceptedAnswerSource? finalSource = accepted
            ? acceptedSource
            : managerAnswer is not null
                ? AcceptedAnswerSource.Superior
                : selfAnswer is not null ? AcceptedAnswerSource.Autoevaluacion : null;

        var finalAnswer = finalSource switch
        {
            AcceptedAnswerSource.Superior => managerAnswer,
            AcceptedAnswerSource.Autoevaluacion => selfAnswer,
            _ => null
        };

        return new ResultQuestionSnapshot(
            question.Id,
            question.Texto,
            question.Tipo,
            question.Topic,
            question.Orden,
            selfAnswer,
            managerAnswer,
            finalAnswer,
            finalAnswer is null ? null : finalSource,
            levels.GetValueOrDefault(question.Id, AlignmentLevel.NoComparable),
            accepted
        );
    }

    private string? DecryptAnswer(EvaluationSubmission submission, int questionId)
    {
        var answer = submission.Answers.FirstOrDefault(a => a.QuestionId == questionId);
        if (answer is null || string.IsNullOrEmpty(answer.EncryptedPayload)) return null;

        try
        {
            return encryptionService.Decrypt(answer.EncryptedPayload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CompleteEvaluationCycle: no se pudo descifrar la respuesta {AnswerId} de la submission {SubmissionId}.",
                answer.Id, submission.Id);
            return null;
        }
    }

    private static string? FormatAnswer(Question question, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        if (question.Tipo != QuestionType.Seleccion) return raw.Trim();

        try
        {
            var options = JsonSerializer.Deserialize<List<string>>(raw);
            return options is null || options.Count == 0 ? null : string.Join(", ", options);
        }
        catch (JsonException)
        {
            return raw.Trim();
        }
    }

    private static int? ParseNumber(string? raw)
    {
        return int.TryParse(raw?.Trim(), out var value) ? value : null;
    }

    private static double? AverageOrNull(IEnumerable<int?> values)
    {
        var list = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return list.Count == 0 ? null : Math.Round(list.Average(), 2, MidpointRounding.AwayFromZero);
    }

    private async Task NotifyEmployeesAsync(List<EvaluationSubmission> submissions, string cycleName, CancellationToken cancellationToken)
    {
        var employees = submissions
            .Select(s => s.EvaluatedUser!)
            .DistinctBy(u => u.Id)
            .ToList();

        foreach (var employee in employees)
        {
            try
            {
                var body = await BuildEmailBodyAsync(employee.Nombre, cycleName, cancellationToken);
                await emailService.SendEmailAsync(employee.Email, "Tus resultados de evaluación están disponibles - EvalFlow", body, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CompleteEvaluationCycle: no se pudo encolar el email de resultados para el usuario {UserId}.", employee.Id);
            }
        }
    }

    private async Task<string> BuildEmailBodyAsync(string userNombre, string cycleName, CancellationToken cancellationToken)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Notifications", "Templates", "EvaluationResultsReadyEmail.html");
        var link = frontendSettings.Value.BuildLink("dashboard/resultados-evaluacion");

        if (!File.Exists(templatePath))
        {
            return $"<p>Hola {WebUtility.HtmlEncode(userNombre)},</p><p>Ya puedes descargar tu informe de resultados del ciclo <b>{WebUtility.HtmlEncode(cycleName)}</b>.</p><p><a href='{link}'>Ver mis resultados</a></p>";
        }

        var template = await File.ReadAllTextAsync(templatePath, cancellationToken);

        return template
            .Replace("{{UserNombre}}", WebUtility.HtmlEncode(userNombre))
            .Replace("{{CycleName}}", WebUtility.HtmlEncode(cycleName))
            .Replace("{{Link}}", link)
            .Replace("{{Year}}", DateTime.UtcNow.Year.ToString());
    }
}
