using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Team.AssignJobPosition;

public record AssignJobPositionBody(int? JobPositionId);

public record AssignJobPositionRecord(int UserId, int? JobPositionId) : IRequest<IResult>;