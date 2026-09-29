using System.Text.Json;
using evalflow_backend_api.Features.AiAnalysis.AiAnalysisDto;
using evalflow_backend_api.Infrastructure.AI;

namespace evalflow_backend_api.Features.AiAnalysis;

/// <summary>
/// Turns the model's JSON into an <see cref="AiAnalysisResultDto"/>. Providers without strict
/// schema support may wrap it in a code fence, omit lists or invent enum values, so everything
/// is normalised instead of trusted.
/// </summary>
public static class AiAnalysisResultParser
{
    private const int MaxItems = 5;

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static AiAnalysisResultDto Parse(string content)
    {
        RawResult? raw;
        try
        {
            raw = JsonSerializer.Deserialize<RawResult>(StripCodeFence(content), Options);
        }
        catch (JsonException ex)
        {
            throw new LlmException($"La IA devolvió un JSON no válido: {ex.Message}", true);
        }

        if (raw is null || string.IsNullOrWhiteSpace(raw.Resumen))
            throw new LlmException("La IA devolvió un análisis sin resumen.", true);

        return new AiAnalysisResultDto(
            raw.Resumen.Trim(),
            OneOf(raw.NivelRiesgo, AiAnalysisPrompt.RiskLevels, "Medio"),
            Texts(raw.Fortalezas),
            Texts(raw.AreasDeMejora),
            (raw.CausasProbables ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c.Explicacion))
                .Take(MaxItems)
                .Select(c => new AiProbableCauseDto(
                    c.Tema?.Trim() ?? "General",
                    c.PreguntaIds ?? [],
                    OneOf(c.Causa, AiAnalysisPrompt.Causes, "Otro"),
                    c.Explicacion!.Trim(),
                    Texts(c.Evidencia),
                    OneOf(c.Confianza, AiAnalysisPrompt.Confidences, "Media")))
                .ToList(),
            Texts(raw.Patrones),
            (raw.Recomendaciones ?? [])
                .Where(r => !string.IsNullOrWhiteSpace(r.Accion))
                .Take(MaxItems)
                .Select(r => new AiRecommendationDto(OneOf(r.Destinatario, AiAnalysisPrompt.Recipients, "RRHH"), r.Accion!.Trim()))
                .ToList(),
            (raw.SolicitudesSugeridas ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s.Mensaje))
                .Take(3)
                .Select(s => new AiSuggestedClarificationDto(s.PreguntaId, Truncate(s.Mensaje!.Trim(), 1000)))
                .ToList(),
            Texts(raw.Limitaciones));
    }

    private static string StripCodeFence(string content)
    {
        var text = content.Trim();
        if (!text.StartsWith("```")) return text;

        var start = text.IndexOf('\n');
        var end = text.LastIndexOf("```", StringComparison.Ordinal);
        return start >= 0 && end > start ? text[(start + 1)..end].Trim() : text.Trim('`');
    }

    private static List<string> Texts(List<string>? values) =>
        (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Take(MaxItems).ToList();

    private static string OneOf(string? value, string[] allowed, string fallback) =>
        allowed.FirstOrDefault(a => string.Equals(a, value?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? fallback;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private record RawResult(
        string? Resumen,
        string? NivelRiesgo,
        List<string>? Fortalezas,
        List<string>? AreasDeMejora,
        List<RawCause>? CausasProbables,
        List<string>? Patrones,
        List<RawRecommendation>? Recomendaciones,
        List<RawClarification>? SolicitudesSugeridas,
        List<string>? Limitaciones);

    private record RawCause(string? Tema, List<int>? PreguntaIds, string? Causa, string? Explicacion, List<string>? Evidencia, string? Confianza);

    private record RawRecommendation(string? Destinatario, string? Accion);

    private record RawClarification(int? PreguntaId, string? Mensaje);
}
