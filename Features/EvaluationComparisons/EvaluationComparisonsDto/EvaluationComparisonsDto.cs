using evalflow_backend_api.Domain.Enums;

namespace evalflow_backend_api.Features.EvaluationComparisons.EvaluationComparisonsDto;

public record QuestionComparisonDto(
    int QuestionId,
    string Texto,
    QuestionType Tipo,
    string Topic,
    int Orden,
    int? SelfValue,
    int? ManagerValue,
    List<string>? SelfOptions,
    List<string>? ManagerOptions,
    int? Gap,
    AlignmentLevel Level,
    GapDirection Direction,
    AcceptedAnswerSource? AcceptedSource
);

public record TopicComparisonDto(
    string Topic,
    int NumericQuestions,
    double? AverageSelf,
    double? AverageManager,
    double? AverageGap,
    AlignmentLevel Level,
    GapDirection Direction
);

public record ComparisonSummaryDto(
    int TotalQuestions,
    int Alineadas,
    int Leves,
    int Desequilibrios,
    int NoComparables,
    double? AlignmentPercentage,
    double? AverageSelf,
    double? AverageManager,
    double? AverageAbsoluteGap,
    bool HasImbalances
);

public record EmployeeComparisonDto(
    int EvaluatedUserId,
    string EvaluatedUserName,
    string EvaluatedRol,
    int TemplateId,
    string TemplateTitle,
    int? ManagerUserId,
    string? ManagerName,
    bool SelfCompleted,
    bool ManagerCompleted,
    bool IsComparable,
    ComparisonSummaryDto? Summary,
    List<TopicComparisonDto> Topics,
    List<QuestionComparisonDto> Questions,
    int PendingImbalances
);

public record CycleComparisonsDto(
    int CycleId,
    string CycleName,
    bool IsCompleted,
    DateTime? CompletedAt,
    int PendingImbalances,
    List<EmployeeComparisonDto> Comparisons
);
