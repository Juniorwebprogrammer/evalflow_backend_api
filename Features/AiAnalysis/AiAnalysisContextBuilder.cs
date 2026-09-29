using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.AiAnalysis.AiAnalysisDto;
using evalflow_backend_api.Features.EvaluationComparisons.EvaluationComparisonsDto;

namespace evalflow_backend_api.Features.AiAnalysis;

/// <summary>A clarification with its responses already decrypted.</summary>
public record ClarificationForAnalysis(int? QuestionId, string Mensaje, string? EvaluatedResponse, string? ManagerResponse, ClarificationStatus Estado);

/// <summary>
/// Builds the pseudonymised input of one employee's analysis. Names never leave EvalFlow:
/// people are "el evaluado" / "el evaluador", and their names are masked in free text too.
/// </summary>
public static class AiAnalysisContextBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Plain accents instead of \u00F3 escapes: fewer tokens and easier for the model to read.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static AiAnalysisContext Build(EvaluationType tipo, EmployeeComparisonDto comparison,
        IEnumerable<ClarificationForAnalysis> clarifications)
    {
        var mask = BuildMask(comparison.EvaluatedUserName, comparison.ManagerName);

        return new AiAnalysisContext(
            TypeLabel(tipo),
            mask(comparison.TemplateTitle),
            comparison.EvaluatedRol,
            comparison.Summary is { } s
                ? new AiContextSummary(s.TotalQuestions, s.Alineadas, s.Leves, s.Desequilibrios, s.AlignmentPercentage,
                    s.AverageSelf, s.AverageManager, s.AverageAbsoluteGap)
                : null,
            comparison.Topics
                .Select(t => new AiContextTopic(mask(t.Topic), t.AverageSelf, t.AverageManager, t.AverageGap, t.Level.ToString()))
                .ToList(),
            comparison.Questions
                .OrderBy(q => q.Orden)
                .Select(q => new AiContextQuestion(
                    q.QuestionId,
                    mask(q.Texto),
                    mask(q.Topic),
                    q.Tipo == QuestionType.Seleccion ? "Seleccion" : "Escala 1-5",
                    q.SelfValue,
                    q.ManagerValue,
                    q.SelfOptions,
                    q.ManagerOptions,
                    q.Gap,
                    q.Level.ToString(),
                    q.Direction.ToString(),
                    q.AcceptedSource?.ToString()))
                .ToList(),
            clarifications
                .Select(c => new AiContextClarification(
                    c.QuestionId,
                    mask(c.Mensaje),
                    c.EvaluatedResponse is null ? null : mask(c.EvaluatedResponse),
                    c.ManagerResponse is null ? null : mask(c.ManagerResponse),
                    c.Estado.ToString()))
                .ToList());
    }

    public static string Serialize(AiAnalysisContext context) => JsonSerializer.Serialize(context, JsonOptions);

    public static string Hash(string serializedContext) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serializedContext))).ToLowerInvariant();

    private static string TypeLabel(EvaluationType tipo) => tipo switch
    {
        EvaluationType.Evaluacion360 => "360 (autoevaluación + superior)",
        EvaluationType.Evaluacion180 => "180 (solo superior)",
        _ => "Autoevaluación",
    };

    /// <summary>Replaces the evaluated person's and the evaluator's full name and name parts (3+ letters).</summary>
    internal static Func<string, string> BuildMask(string evaluatedName, string? managerName)
    {
        var replacements = Parts(evaluatedName).Select(p => (p, "[evaluado]"))
            .Concat(Parts(managerName).Select(p => (p, "[evaluador]")))
            .OrderByDescending(r => r.p.Length)
            .ToList();

        if (replacements.Count == 0) return text => text;

        return text => replacements.Aggregate(text, (current, r) =>
            Regex.Replace(current, $@"\b{Regex.Escape(r.p)}\b", r.Item2, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    }

    private static IEnumerable<string> Parts(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return [];

        var name = fullName.Trim();
        return name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length >= 3)
            .Prepend(name)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
