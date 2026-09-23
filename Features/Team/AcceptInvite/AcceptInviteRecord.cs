using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Team.AcceptInvite;

public record AcceptInviteRecord(
    string Token, 
    string Nombre, 
    string Apellidos, 
    string Password
) : IRequest<IResult>;