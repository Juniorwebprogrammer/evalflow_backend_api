using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Team.ToggleUserStatus;

public record ToggleUserStatusBody(bool Activo);

public record ToggleUserStatusRecord(int UserId, bool Activo) : IRequest<IResult>;