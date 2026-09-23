using MediatR;

namespace evalflow_backend_api.Features.Team.GetEmployees;

public record GetEmployeeRecord() : IRequest<IResult>;

public record GetEmployeeResponse(
    string Id,
    string Nombre,
    string Apellido,
    string Email,
    string Rol,
    string Cargo,
    bool Activo,
    DateTime FechaCreacion
);