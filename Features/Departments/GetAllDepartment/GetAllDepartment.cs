using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Departments.GetAllDepartment;

public record GetAllDepartmentsRecord() : IRequest<IResult>;

public record DepartmentSummaryDto(
    int Id, 
    string Nombre, 
    string? Descripcion, 
    DateTime FechaCreacion, 
    int EmployeeCount 
);