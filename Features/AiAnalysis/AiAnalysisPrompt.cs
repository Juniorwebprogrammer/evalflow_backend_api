using evalflow_backend_api.Domain.Enums;

namespace evalflow_backend_api.Features.AiAnalysis;

/// <summary>System prompt and JSON schema of the evaluation analysis.</summary>
public static class AiAnalysisPrompt
{
    public const string SchemaName = "evaluation_analysis";

    public static readonly string[] RiskLevels = ["Bajo", "Medio", "Alto"];
    public static readonly string[] Confidences = ["Baja", "Media", "Alta"];
    public static readonly string[] Recipients = ["RRHH", "Evaluado", "Evaluador"];
    public static readonly string[] Causes =
    [
        "Expectativas no alineadas", "Falta de feedback", "Desconocimiento del rol", "Sesgo de autopercepción",
        "Evidencia insuficiente", "Diferente criterio de valoración", "Contexto o carga de trabajo", "Otro",
    ];

    public static string SystemPrompt(EvaluationType tipo)
    {
        var focus = tipo switch
        {
            EvaluationType.Evaluacion360 =>
                """
                This is a 360 evaluation: the employee assessed themselves and their manager also assessed them.
                Your main goal is to explain the IMBALANCES between both views (questions with level
                "Desequilibrio" or "Leve"): why they may exist, what evidence supports it and how to resolve them.
                "Sobrevaloracion" means the employee rated themselves above their manager; "Infravaloracion", below.
                Fill "causasProbables" with one entry per topic or group of imbalanced questions.
                """,
            EvaluationType.Evaluacion180 =>
                """
                This is a 180 evaluation: only the manager assessed the employee, there is no self-assessment to compare with.
                Analyze strengths, areas for improvement, topics with atypical scores and the consistency of the answers.
                Use "causasProbables" to explain the possible causes of the lowest scores.
                """,
            _ =>
                """
                This is a self-assessment: only the employee answered, there is no other view to compare with.
                Analyze how the employee sees themselves: strengths, areas for improvement, topics with atypical scores and
                possible self-perception biases. Use "causasProbables" to explain the lowest or most extreme scores.
                """,
        };

        return $$"""
            You are an expert analyst in performance management and human resources. You analyze the results of an
            EvalFlow performance evaluation and help HR understand them.

            {{focus}}

            Data you receive (JSON, with Spanish field names and labels):
            - Questions with their text, topic and answers. Numeric scales go from 1 to 5.
            - "brecha" = self-assessment score - manager score.
            - "solicitudesInformacion": clarifications HR requested from the employee and the evaluator, with their answers.
              They are the most valuable qualitative evidence: use them to explain the causes and cite them in "evidencia".
            - "respuestaAceptadaPorRrhh": which view HR chose as final for an imbalance.

            Rules:
            - ALWAYS write every free-text value in English, even when the questions, answers or clarifications are in
              another language (translate any quotes you cite). Respond ONLY with the requested JSON.
            - Keep the JSON property names exactly as defined in the schema, and copy enum values exactly as listed
              (they are fixed codes, not prose — do not translate them).
            - Base yourself only on the data received. Do not invent facts; if something is a hypothesis, say so and adjust "confianza".
            - People are pseudonymized ("[evaluado]", "[evaluador]"). Refer to them as "the employee" and "the evaluator",
              and do not try to identify them.
            - Do not make judgments about the person or psychological diagnoses; talk about behaviors and results.
            - Reference questions by their "id" in "preguntaIds" and "preguntaId".
            - "causa" must be one of: {{string.Join(", ", Causes)}}.
            - Suggest in "solicitudesSugeridas" concrete questions HR could ask when information is missing
              to explain a gap (maximum 3). Do not repeat requests that were already made.
            - Note in "limitaciones" anything that reduces the reliability of the analysis (few questions, no clarifications, etc.).
            - Be concise: "resumen" of 2 to 4 sentences; at most 5 items per list.
            """;
    }

    private static object StringArray => new { type = "array", items = new { type = "string" } };

    private static object Enum(string[] values) => new { type = "string", @enum = values };

    public static readonly object Schema = new
    {
        type = "object",
        additionalProperties = false,
        required = new[]
        {
            "resumen", "nivelRiesgo", "fortalezas", "areasDeMejora", "causasProbables", "patrones",
            "recomendaciones", "solicitudesSugeridas", "limitaciones",
        },
        properties = new
        {
            resumen = new { type = "string" },
            nivelRiesgo = Enum(RiskLevels),
            fortalezas = StringArray,
            areasDeMejora = StringArray,
            causasProbables = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "tema", "preguntaIds", "causa", "explicacion", "evidencia", "confianza" },
                    properties = new
                    {
                        tema = new { type = "string" },
                        preguntaIds = new { type = "array", items = new { type = "integer" } },
                        causa = Enum(Causes),
                        explicacion = new { type = "string" },
                        evidencia = StringArray,
                        confianza = Enum(Confidences),
                    },
                },
            },
            patrones = StringArray,
            recomendaciones = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "destinatario", "accion" },
                    properties = new { destinatario = Enum(Recipients), accion = new { type = "string" } },
                },
            },
            solicitudesSugeridas = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "preguntaId", "mensaje" },
                    properties = new
                    {
                        preguntaId = new { type = new[] { "integer", "null" } },
                        mensaje = new { type = "string" },
                    },
                },
            },
            limitaciones = StringArray,
        },
    };
}
