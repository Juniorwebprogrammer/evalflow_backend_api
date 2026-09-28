using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationResults.EvaluationResultsDto;

namespace evalflow_backend_api.Features.EvaluationResults.DownloadEvaluationResultPdf;

/// <summary>Average scores (1–5) of one topic, per respondent.</summary>
public record TopicScore(string Topic, double? Self, double? Manager, double? Final, int QuestionCount);

/// <summary>A numeric question with its final score, for the strengths / improvement lists.</summary>
public record ScoredQuestion(string Texto, string Topic, int Score);

/// <summary>
/// Everything the PDF shows beyond the raw snapshot: per-topic averages, the
/// alignment breakdown, the distribution of final scores and the strongest /
/// weakest questions. Only numeric questions (stars, 1–5 scale) take part in
/// averages; selection questions are listed but never scored.
/// </summary>
public class EvaluationResultReport
{
    public const int MaxScore = 5;
    /// <summary>Questions listed as strengths / improvement areas.</summary>
    private const int HighlightCount = 3;

    public EvaluationResultSnapshot Snapshot { get; }
    public bool ShowSelf { get; }
    public bool ShowManager { get; }
    public bool ShowAlignment { get; }
    public IReadOnlyList<TopicScore> Topics { get; }
    /// <summary>Questions per alignment level, only for comparable (360°) cycles.</summary>
    public IReadOnlyDictionary<AlignmentLevel, int> AlignmentCounts { get; }
    /// <summary>How many questions got each final score, index 0 = score 1.</summary>
    public IReadOnlyList<int> FinalDistribution { get; }
    public IReadOnlyList<ScoredQuestion> Strengths { get; }
    public IReadOnlyList<ScoredQuestion> Improvements { get; }
    public int AnsweredCount { get; }
    public int AcceptedCount { get; }

    public EvaluationResultReport(EvaluationResultSnapshot snapshot)
    {
        Snapshot = snapshot;
        ShowSelf = snapshot.TipoEvaluacion != EvaluationType.Evaluacion180;
        ShowManager = snapshot.TipoEvaluacion != EvaluationType.Auto;
        ShowAlignment = snapshot.TipoEvaluacion == EvaluationType.Evaluacion360;

        var questions = snapshot.Questions.OrderBy(q => q.Orden).ToList();
        var numeric = questions.Where(IsNumeric).ToList();

        Topics = numeric
            .GroupBy(q => string.IsNullOrWhiteSpace(q.Topic) ? "General" : q.Topic.Trim())
            .Select(g => new TopicScore(
                g.Key,
                Average(g.Select(q => Score(q.SelfAnswer))),
                Average(g.Select(q => Score(q.ManagerAnswer))),
                Average(g.Select(q => Score(q.FinalAnswer))),
                g.Count()))
            .ToList();

        AlignmentCounts = ShowAlignment
            ? questions
                .GroupBy(q => q.Level)
                .ToDictionary(g => g.Key, g => g.Count())
            : new Dictionary<AlignmentLevel, int>();

        var finals = numeric
            .Select(q => (Question: q, Score: Score(q.FinalAnswer)))
            .Where(x => x.Score.HasValue)
            .Select(x => new ScoredQuestion(x.Question.Texto, x.Question.Topic, x.Score!.Value))
            .ToList();

        FinalDistribution = Enumerable.Range(1, MaxScore).Select(s => finals.Count(f => f.Score == s)).ToList();
        Strengths = finals.Where(f => f.Score >= 4).OrderByDescending(f => f.Score).Take(HighlightCount).ToList();
        Improvements = finals.Where(f => f.Score <= 3).OrderBy(f => f.Score).Take(HighlightCount).ToList();

        AnsweredCount = questions.Count(q => q.FinalAnswer is not null);
        AcceptedCount = questions.Count(q => q.Accepted);
    }

    public static bool IsNumeric(ResultQuestionSnapshot question) => question.Tipo != QuestionType.Seleccion;

    /// <summary>Parses a numeric answer, clamped to the 1–5 scale; null when missing or not a number.</summary>
    public static int? Score(string? answer) =>
        int.TryParse(answer?.Trim(), out var value) && value is >= 1 and <= MaxScore ? value : null;

    private static double? Average(IEnumerable<int?> values)
    {
        var list = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return list.Count == 0 ? null : Math.Round(list.Average(), 2, MidpointRounding.AwayFromZero);
    }
}
