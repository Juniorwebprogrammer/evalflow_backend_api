using System.Text.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationComparisons.EvaluationComparisonsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationComparisons.GetCycleComparisons;

public class GetCycleComparisonsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService,
    ILogger<GetCycleComparisonsHandler> logger
) : IRequestHandler<GetCycleComparisonsRecord, IResult>
{
    private const double LeveFrom = 1;
    private const double DesequilibrioFrom = 2;

    public async Task<IResult> Handle(GetCycleComparisonsRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var cycle = await GetCycleAsync(request.CycleId, tenantId, cancellationToken);
        if (cycle is null) return Results.NotFound(new { Message = "Ciclo no encontrado." });

        if (cycle.TipoEvaluación != EvaluationType.Evaluacion360)
        {
            return Results.BadRequest(new
            {
                Message = "La comparación solo está disponible en ciclos 360 (autoevaluación + evaluación del superior)."
            });
        }

        var submissions = await FetchSubmissionsAsync(cycle.Id, request.EvaluatedUserId, cancellationToken);

        var comparisons = BuildComparisons(submissions);

        return Results.Ok(new CycleComparisonsDto(cycle.Id, cycle.Nombre, comparisons));
    }

    private async Task<EvaluationCycle?> GetCycleAsync(int cycleId, string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.Empresa!.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<List<EvaluationSubmission>> FetchSubmissionsAsync(int cycleId, int? evaluatedUserId, CancellationToken cancellationToken)
    {
        var query = dbContext.EvaluationSubmissions
            .AsNoTracking()
            .Where(s => s.EvaluationCycleId == cycleId);

        if (evaluatedUserId.HasValue)
            query = query.Where(s => s.EvaluatedUserId == evaluatedUserId.Value);

        return await query
            .Include(s => s.Answers)
            .Include(s => s.EvaluatedUser)
            .Include(s => s.RespondentUser)
            .Include(s => s.Template)
                .ThenInclude(t => t!.Preguntas)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    private List<EmployeeComparisonDto> BuildComparisons(List<EvaluationSubmission> submissions)
    {
        return submissions
            .GroupBy(s => new { s.EvaluatedUserId, s.TemplateId })
            .Select(group => BuildEmployeeComparison(group.ToList()))
            .OrderBy(c => c.EvaluatedUserName)
            .ThenBy(c => c.TemplateTitle)
            .ToList();
    }

    private EmployeeComparisonDto BuildEmployeeComparison(List<EvaluationSubmission> submissions)
    {
        var reference = submissions[0];
        var self = FindSelfSubmission(submissions);
        var manager = FindManagerSubmission(submissions);

        var selfCompleted = self?.IsCompleted == true;
        var managerCompleted = manager?.IsCompleted == true;
        var isComparable = selfCompleted && managerCompleted;

        var questions = isComparable ? CompareAnswers(reference.Template!, self!, manager!) : [];

        return new EmployeeComparisonDto(
            reference.EvaluatedUserId,
            $"{reference.EvaluatedUser!.Nombre} {reference.EvaluatedUser.Apellidos}",
            reference.EvaluatedUser.Rol,
            reference.TemplateId,
            reference.Template!.Titulo,
            manager?.RespondentUserId,
            manager is null ? null : $"{manager.RespondentUser!.Nombre} {manager.RespondentUser.Apellidos}",
            selfCompleted,
            managerCompleted,
            isComparable,
            isComparable ? BuildSummary(questions) : null,
            isComparable ? BuildTopics(questions) : [],
            questions
        );
    }

    private static EvaluationSubmission? FindSelfSubmission(List<EvaluationSubmission> submissions)
    {
        return submissions.FirstOrDefault(s => s.RespondentUserId == s.EvaluatedUserId);
    }

    private static EvaluationSubmission? FindManagerSubmission(List<EvaluationSubmission> submissions)
    {
        return submissions
            .Where(s => s.RespondentUserId != s.EvaluatedUserId)
            .OrderByDescending(s => s.IsCompleted)
            .ThenByDescending(s => s.SubmittedAt)
            .FirstOrDefault();
    }

    private List<QuestionComparisonDto> CompareAnswers(Template template, EvaluationSubmission self, EvaluationSubmission manager)
    {
        return template.Preguntas
            .OrderBy(q => q.Orden)
            .Select(question => CompareQuestion(
                question,
                DecryptAnswer(self, question.Id),
                DecryptAnswer(manager, question.Id)))
            .ToList();
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
            logger.LogWarning(ex, "GetCycleComparisons: no se pudo descifrar la respuesta {AnswerId} de la submission {SubmissionId}.",
                answer.Id, submission.Id);
            return null;
        }
    }

    private static QuestionComparisonDto CompareQuestion(Question question, string? selfRaw, string? managerRaw)
    {
        return question.Tipo == QuestionType.Seleccion
            ? CompareSelection(question, selfRaw, managerRaw)
            : CompareNumeric(question, selfRaw, managerRaw);
    }

    private static QuestionComparisonDto CompareNumeric(Question question, string? selfRaw, string? managerRaw)
    {
        var self = ParseNumber(selfRaw);
        var manager = ParseNumber(managerRaw);

        if (self is null || manager is null)
            return MapQuestion(question, self, manager, null, null, null, AlignmentLevel.NoComparable, GapDirection.Ninguna);

        var gap = self.Value - manager.Value;

        return MapQuestion(question, self, manager, null, null, gap, ClassifyGap(Math.Abs(gap)), GetDirection(gap));
    }

    private static QuestionComparisonDto CompareSelection(Question question, string? selfRaw, string? managerRaw)
    {
        var self = ParseOptions(selfRaw);
        var manager = ParseOptions(managerRaw);

        if (self is null || manager is null)
            return MapQuestion(question, null, null, self, manager, null, AlignmentLevel.NoComparable, GapDirection.Ninguna);

        var level = self.ToHashSet(StringComparer.Ordinal).SetEquals(manager)
            ? AlignmentLevel.Alineado
            : AlignmentLevel.Desequilibrio;

        return MapQuestion(question, null, null, self, manager, null, level, GapDirection.Ninguna);
    }

    private static QuestionComparisonDto MapQuestion(Question question, int? selfValue, int? managerValue,
        List<string>? selfOptions, List<string>? managerOptions, int? gap, AlignmentLevel level, GapDirection direction)
    {
        return new QuestionComparisonDto(
            question.Id,
            question.Texto,
            question.Tipo,
            question.Topic,
            question.Orden,
            selfValue,
            managerValue,
            selfOptions,
            managerOptions,
            gap,
            level,
            direction
        );
    }

    private static AlignmentLevel ClassifyGap(double absoluteGap)
    {
        var rounded = Math.Round(absoluteGap, MidpointRounding.AwayFromZero);

        if (rounded >= DesequilibrioFrom) return AlignmentLevel.Desequilibrio;
        if (rounded >= LeveFrom) return AlignmentLevel.Leve;

        return AlignmentLevel.Alineado;
    }

    private static GapDirection GetDirection(double gap)
    {
        if (gap > 0) return GapDirection.Sobrevaloracion;
        if (gap < 0) return GapDirection.Infravaloracion;

        return GapDirection.Ninguna;
    }

    private static ComparisonSummaryDto BuildSummary(List<QuestionComparisonDto> questions)
    {
        var alineadas = questions.Count(q => q.Level == AlignmentLevel.Alineado);
        var leves = questions.Count(q => q.Level == AlignmentLevel.Leve);
        var desequilibrios = questions.Count(q => q.Level == AlignmentLevel.Desequilibrio);
        var noComparables = questions.Count(q => q.Level == AlignmentLevel.NoComparable);
        var comparables = questions.Count - noComparables;

        var numeric = questions.Where(q => q.Gap.HasValue).ToList();

        return new ComparisonSummaryDto(
            questions.Count,
            alineadas,
            leves,
            desequilibrios,
            noComparables,
            comparables == 0 ? null : Round(100.0 * (alineadas + leves) / comparables),
            AverageOrNull(numeric.Select(q => q.SelfValue!.Value)),
            AverageOrNull(numeric.Select(q => q.ManagerValue!.Value)),
            AverageOrNull(numeric.Select(q => Math.Abs(q.Gap!.Value))),
            desequilibrios > 0
        );
    }

    private static List<TopicComparisonDto> BuildTopics(List<QuestionComparisonDto> questions)
    {
        return questions
            .Where(q => q.Gap.HasValue)
            .GroupBy(q => q.Topic)
            .Select(group => MapTopic(group.Key, group.ToList()))
            .OrderByDescending(t => Math.Abs(t.AverageGap ?? 0))
            .ThenBy(t => t.Topic)
            .ToList();
    }

    private static TopicComparisonDto MapTopic(string topic, List<QuestionComparisonDto> questions)
    {
        var averageGap = Round(questions.Average(q => (double)q.Gap!.Value));

        return new TopicComparisonDto(
            topic,
            questions.Count,
            Round(questions.Average(q => q.SelfValue!.Value)),
            Round(questions.Average(q => q.ManagerValue!.Value)),
            averageGap,
            ClassifyGap(Math.Abs(averageGap)),
            GetDirection(averageGap)
        );
    }

    private static int? ParseNumber(string? raw)
    {
        return int.TryParse(raw?.Trim(), out var value) ? value : null;
    }

    private static List<string>? ParseOptions(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        try
        {
            return JsonSerializer.Deserialize<List<string>>(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static double? AverageOrNull(IEnumerable<int> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? null : Round(list.Average());
    }

    private static double Round(double value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
