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
    DateTime FechaCreacion,
    EmployeeDepartmentResponse? Departamento,
    EmployeeSuperiorResponse? Superior
);

public record EmployeeDepartmentResponse(int Id, string Nombre);
public record EmployeeSuperiorResponse(int Id, string Nombre, string Apellidos);