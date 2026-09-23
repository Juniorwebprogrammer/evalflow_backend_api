namespace evalflow_backend_api.Features.EvaluationSubmissions.EvaluationSubmissionsDto;

public record PendingSubmissionDto(
    int SubmissionId, 
    string TemplateTitle, 
    string CycleName, 
    string EvaluatedUserName, 
    DateTime FechaFinCiclo
);

public record AnswerInputDto(
    int QuestionId, 
    string RawPayload
);

public record SubmissionQuestionDto(
    int QuestionId,
    string Texto,
    Domain.Enums.QuestionType Tipo,
    List<string>? Opciones,
    int Orden
);

public record SubmissionDetailDto(
    int SubmissionId,
    bool IsCompleted,
    string CycleName,
    string EvaluatedUserName,
    string TemplateTitle,
    string? TemplateDescription,
    List<SubmissionQuestionDto> Questions,
    DateTime FechaFinCiclo
);

/// <summary>
/// One submission of a cycle, for Owner/Rrhh to see who has to answer,
/// who's being evaluated, and whether it's done — returned by
/// <c>GET /evaluation-cycles/{cycleId}/submissions</c>.
/// </summary>
public record CycleSubmissionDto(
    int SubmissionId,
    int RespondentUserId,
    string RespondentUserName,
    int EvaluatedUserId,
    string EvaluatedUserName,
    string TemplateTitle,
    bool IsCompleted,
    DateTime? SubmittedAt
);