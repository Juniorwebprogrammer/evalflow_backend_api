using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Team.AssignSuperior;

public record AssignSuperiorBody(int? SuperiorId);
public record AssignSuperiorRecord(int UserId, int? SuperiorId) : IRequest<IResult>;