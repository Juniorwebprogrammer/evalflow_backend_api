using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Departments.CreateDepartment;

public record CreateDepartmentRecord(string Nombre, string? Descripcion) : IRequest<IResult>;