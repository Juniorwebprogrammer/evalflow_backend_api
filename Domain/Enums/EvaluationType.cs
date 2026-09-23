namespace evalflow_backend_api.Domain.Enums;

public enum EvaluationType
{
    Auto = 0, // Sólo autoevaluación
    Evaluacion180 = 1, // Sólo el evaluador evalúa al subordinado
    Evaluacion360 = 2 // Evaluador evalúa al subordinado + autoevaluación
}