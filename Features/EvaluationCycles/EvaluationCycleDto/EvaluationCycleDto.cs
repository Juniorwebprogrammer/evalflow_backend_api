using evalflow_backend_api.Domain.Enums;

namespace evalflow_backend_api.Features.EvaluationCycles;

public record EvaluationCycleSummaryDto(
    int Id,
    string Nombre,
    bool Activo,
    DateTime FechaInicio,
    DateTime FechaFin,
    int TemplatesCount,
    List<int> TemplateIds,
    EvaluationType TipoEvaluacion,
    DateTime? FechaCompletado
);