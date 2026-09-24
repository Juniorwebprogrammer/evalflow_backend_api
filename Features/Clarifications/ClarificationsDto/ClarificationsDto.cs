using evalflow_backend_api.Domain.Enums;

namespace evalflow_backend_api.Features.Clarifications.ClarificationsDto;

public record ClarificationDto(
    int Id,
    int CycleId,
    int TemplateId,
    string TemplateTitle,
    int? QuestionId,
    string? QuestionText,
    int EvaluatedUserId,
    string EvaluatedUserName,
    int ManagerUserId,
    string ManagerName,
    string RequestedByName,
    string Mensaje,
    string? EvaluatedResponse,
    DateTime? EvaluatedRespondedAt,
    string? ManagerResponse,
    DateTime? ManagerRespondedAt,
    ClarificationStatus Estado,
    DateTime FechaCreacion
);

public record MyClarificationDto(
    int Id,
    string CycleName,
    string TemplateTitle,
    string? QuestionText,
    string EvaluatedUserName,
    ClarificationParticipant MyRole,
    string RequestedByName,
    string Mensaje,
    string? MyResponse,
    DateTime? MyRespondedAt,
    DateTime FechaCreacion
);
