using evalflow_backend_api.Domain.Enums;

namespace evalflow_backend_api.Features.AiAnalysis.AiAnalysisDto;

/// <summary>The model's answer, as described by <see cref="AiAnalysisPrompt.Schema"/>.</summary>
public record AiAnalysisResultDto(
    string Resumen,
    string NivelRiesgo,
    List<string> Fortalezas,
    List<string> AreasDeMejora,
    List<AiProbableCauseDto> CausasProbables,
    List<string> Patrones,
    List<AiRecommendationDto> Recomendaciones,
    List<AiSuggestedClarificationDto> SolicitudesSugeridas,
    List<string> Limitaciones
);

public record AiProbableCauseDto(
    string Tema,
    List<int> PreguntaIds,
    string Causa,
    string Explicacion,
    List<string> Evidencia,
    string Confianza
);

public record AiRecommendationDto(string Destinatario, string Accion);

public record AiSuggestedClarificationDto(int? PreguntaId, string Mensaje);

public record AiEvaluationAnalysisDto(
    int Id,
    int EvaluationCycleId,
    int TemplateId,
    int EvaluatedUserId,
    AiAnalysisStatus Estado,
    string? Model,
    DateTime FechaCreacion,
    DateTime? FechaCompletado,
    string? ErrorMensaje,
    AiAnalysisResultDto? Result
);

public record RequestAiAnalysisBody(int? EvaluatedUserId, int? TemplateId, bool Force = false);

public record RequestAiAnalysisResponse(int Created, int Reused, List<AiEvaluationAnalysisDto> Analyses);

/// <summary>Body of the 403 returned when the company's plan doesn't include AI.</summary>
public record PlanFeatureError(string Code, string Message, string Feature);

/// <summary>Real-time event sent to the tenant group when an analysis finishes (ok or failed).</summary>
public record AiAnalysisCompletedNotification(int AnalysisId, int CycleId, int TemplateId, int EvaluatedUserId, AiAnalysisStatus Estado);

// ---- Pseudonymised input sent to the model (no names, no emails) ----

public record AiAnalysisContext(
    string TipoEvaluacion,
    string Plantilla,
    string RolEvaluado,
    AiContextSummary? Resumen,
    List<AiContextTopic> Temas,
    List<AiContextQuestion> Preguntas,
    List<AiContextClarification> SolicitudesInformacion
);

public record AiContextSummary(
    int TotalPreguntas,
    int Alineadas,
    int Leves,
    int Desequilibrios,
    double? PorcentajeAlineacion,
    double? MediaAutoevaluacion,
    double? MediaSuperior,
    double? BrechaAbsolutaMedia
);

public record AiContextTopic(string Tema, double? MediaAutoevaluacion, double? MediaSuperior, double? BrechaMedia, string Nivel);

public record AiContextQuestion(
    int Id,
    string Texto,
    string Tema,
    string Tipo,
    int? Autoevaluacion,
    int? Superior,
    List<string>? OpcionesAutoevaluacion,
    List<string>? OpcionesSuperior,
    int? Brecha,
    string Nivel,
    string Direccion,
    string? RespuestaAceptadaPorRrhh
);

public record AiContextClarification(
    int? PreguntaId,
    string MensajeRrhh,
    string? RespuestaEvaluado,
    string? RespuestaEvaluador,
    string Estado
);
