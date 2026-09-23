using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Departments.GetDepartment;

public class GetDepartmentHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetDepartmentRecord, IResult>
{
    public async Task<IResult> Handle(GetDepartmentRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.Unauthorized();
        }

        var companyId = await GetCompanyIdAsync(tenantId, cancellationToken);
        if (companyId is null)
        {
            return Results.Unauthorized();
        }

        var departmentDetails = await FetchDepartmentWithUsersAsync(request.DepartmentId, companyId.Value, cancellationToken);

        if (departmentDetails is null)
        {
            return Results.NotFound(new { Message = "Departamento no encontrado o no pertenece a tu empresa." });
        }

        return GenerateSuccessResponse(departmentDetails);
    }

    private async Task<int?> GetCompanyIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
            
        return company?.Id;
    }

    private async Task<DepartmentDetailsDto?> FetchDepartmentWithUsersAsync(int departmentId, int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.Departments
            .AsNoTracking()
            .Where(d => d.Id == departmentId && d.EmpresaID == companyId)
            .Select(d => new DepartmentDetailsDto(
                d.Id,
                d.Nombre,
                d.Descripcion,
                d.FechaCreacion,
                d.Usuarios.Select(u => new DepartmentUserDto(
                    u.Id,
                    u.Nombre,
                    u.Apellidos,
                    u.Email,
                    u.Rol
                )).ToList()
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IResult GenerateSuccessResponse(DepartmentDetailsDto departmentDetails)
    {
        return Results.Ok(departmentDetails);
    }
}