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
                La evaluación es 360: el empleado se autoevaluó y su superior también lo evaluó.
                Tu objetivo principal es explicar los DESEQUILIBRIOS entre ambas visiones (preguntas con nivel
                "Desequilibrio" o "Leve"): por qué pueden existir, qué evidencia lo respalda y cómo resolverlos.
                "Sobrevaloracion" significa que el empleado se puntuó por encima de su superior; "Infravaloracion", por debajo.
                Rellena "causasProbables" con una entrada por tema o grupo de preguntas desequilibradas.
                """,
            EvaluationType.Evaluacion180 =>
                """
                La evaluación es 180: solo evaluó el superior, no hay autoevaluación con la que comparar.
                Analiza fortalezas, áreas de mejora, temas con puntuaciones atípicas y la coherencia de las respuestas.
                Usa "causasProbables" para explicar las posibles causas de las puntuaciones más bajas.
                """,
            _ =>
                """
                La evaluación es una autoevaluación: solo respondió el empleado, no hay otra visión con la que comparar.
                Analiza cómo se percibe el empleado: fortalezas, áreas de mejora, temas con puntuaciones atípicas y
                posibles sesgos de autopercepción. Usa "causasProbables" para explicar las puntuaciones más bajas o extremas.
                """,
        };

        return $$"""
            Eres un analista experto en gestión del desempeño y recursos humanos. Analizas los resultados de una
            evaluación de desempeño de EvalFlow y ayudas a RRHH a entenderlos.

            {{focus}}

            Datos que recibes (JSON):
            - Preguntas con su texto, tema y respuestas. Las escalas numéricas van de 1 a 5.
            - "brecha" = autoevaluación - superior.
            - "solicitudesInformacion": aclaraciones que RRHH pidió al evaluado y al evaluador, con sus respuestas.
              Son la evidencia cualitativa más valiosa: úsalas para explicar las causas y cítalas en "evidencia".
            - "respuestaAceptadaPorRrhh": qué visión eligió RRHH como definitiva en un desequilibrio.

            Reglas:
            - Responde SIEMPRE en español y SOLO con el JSON pedido.
            - Básate únicamente en los datos recibidos. No inventes hechos; si algo es una hipótesis, dilo y ajusta "confianza".
            - Las personas están seudonimizadas ("el evaluado", "el evaluador"). No intentes identificarlas.
            - No emitas juicios sobre la persona ni diagnósticos psicológicos; habla de conductas y resultados.
            - Referencia las preguntas por su "id" en "preguntaIds" y "preguntaId".
            - "causa" debe ser uno de: {{string.Join(", ", Causes)}}.
            - Sugiere en "solicitudesSugeridas" preguntas concretas que RRHH podría hacer cuando falte información
              para explicar una brecha (máximo 3). No repitas solicitudes ya hechas.
            - Anota en "limitaciones" lo que reduce la fiabilidad del análisis (pocas preguntas, sin aclaraciones, etc.).
            - Sé conciso: "resumen" de 2 a 4 frases; como máximo 5 elementos por lista.
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
