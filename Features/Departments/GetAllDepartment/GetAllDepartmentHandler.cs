using evalflow_backend_api.Features.Departments.GetAllDepartment;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Departments.GetAllDepartments;

public class GetAllDepartmentsHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetAllDepartmentsRecord, IResult>
{
    public async Task<IResult> Handle(GetAllDepartmentsRecord request, CancellationToken cancellationToken)
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

        var departments = await FetchDepartmentsSummaryAsync(companyId.Value, cancellationToken);

        return GenerateSuccessResponse(departments);
    }

    private async Task<int?> GetCompanyIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
            
        return company?.Id;
    }

    private async Task<List<DepartmentSummaryDto>> FetchDepartmentsSummaryAsync(int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.Departments
            .AsNoTracking()
            .Where(d => d.EmpresaID == companyId)
            // 1. Ojo: Movemos el OrderBy AQUÍ, antes de transformarlo
            .OrderBy(d => d.Nombre) 
            .Select(d => new DepartmentSummaryDto(
                d.Id,
                d.Nombre,
                d.Descripcion,
                d.FechaCreacion,
                // 2. Ojo: Usamos Count() con paréntesis (es el método de System.Linq)
                d.Usuarios.Count() 
            ))
            .ToListAsync(cancellationToken);
    }

    private static IResult GenerateSuccessResponse(List<DepartmentSummaryDto> departments)
    {
        return Results.Ok(departments);
    }
}