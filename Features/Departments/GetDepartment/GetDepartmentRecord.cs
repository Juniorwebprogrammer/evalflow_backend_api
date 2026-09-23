using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Departments.GetDepartment;

public record GetDepartmentRecord(int DepartmentId) : IRequest<IResult>;

public record DepartmentUserDto(int Id, string Nombre, string Apellidos, string Email, string Rol);

public record DepartmentDetailsDto(
    int Id, 
    string Nombre, 
    string? Descripcion, 
    DateTime FechaCreacion, 
    List<DepartmentUserDto> Usuarios
);