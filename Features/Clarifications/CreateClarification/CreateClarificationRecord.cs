using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Clarifications.CreateClarification;

public record CreateClarificationBody(int EvaluatedUserId, int TemplateId, int? QuestionId, string Mensaje);

public record CreateClarificationRecord(
    int CycleId, int EvaluatedUserId, int TemplateId, int? QuestionId, string Mensaje) : IRequest<IResult>;
