using evalflow_backend_api.Domain.Enums;

namespace evalflow_backend_api.Features.EvaluationResults.EvaluationResultsDto;

public record EvaluationResultSummaryDto(
    int Id,
    int CycleId,
    string CycleName,
    int TemplateId,
    string TemplateTitle,
    int EvaluatedUserId,
    string EvaluatedUserName,
    DateTime CompletedAt,
    double? AverageFinal
);

public record EvaluationResultSnapshot(
    string CompanyName,
    string CycleName,
    EvaluationType TipoEvaluacion,
    DateTime FechaInicio,
    DateTime FechaFin,
    string TemplateTitle,
    string? TemplateDescription,
    string EvaluatedUserName,
    string EvaluatedUserEmail,
    string? Cargo,
    string? Departamento,
    string? ManagerName,
    bool SelfCompleted,
    bool ManagerCompleted,
    bool AutoCompleted,
    DateTime CompletedAt,
    double? AverageSelf,
    double? AverageManager,
    double? AverageFinal,
    double? AlignmentPercentage,
    List<ResultQuestionSnapshot> Questions
);

public record ResultQuestionSnapshot(
    int QuestionId,
    string Texto,
    QuestionType Tipo,
    string Topic,
    int Orden,
    string? SelfAnswer,
    string? ManagerAnswer,
    string? FinalAnswer,
    AcceptedAnswerSource? FinalSource,
    AlignmentLevel Level,
    bool Accepted
);
