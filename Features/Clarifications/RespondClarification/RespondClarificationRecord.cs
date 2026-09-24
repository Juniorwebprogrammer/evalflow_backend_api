using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Clarifications.RespondClarification;

public record RespondClarificationBody(string Respuesta);

public record RespondClarificationRecord(int ClarificationId, string Respuesta) : IRequest<IResult>;
