using MediatR;

namespace evalflow_backend_api.Features.Team.InviteEmployee;

public record InviteEmployeeRecord(
    string Nombre,
    string Apellidos,
    string Email,
    string Rol
) : IRequest<IResult>;

public record InviteEmployeeResponse(
    string Message,
    string UserId,
    string Email,
    string RolAsignado
);