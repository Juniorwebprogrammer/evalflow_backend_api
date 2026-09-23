using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Departments.CreateDepartment;

public class CreateDepartmentHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<CreateDepartmentRecord, IResult>
{
    public async Task<IResult> Handle(CreateDepartmentRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) 
        {
            return Results.Unauthorized();
        }

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) 
        {
            return Results.BadRequest(new { Message = "Empresa no encontrada." });
        }

        var department = CreateDepartmentEntity(request, company.Id);
        await SaveDepartmentAsync(department, cancellationToken);

        return GenerateSuccessResponse(department.Id);
    }

    private async Task<Company?> GetCompanyAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Companies
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
    }

    private static Department CreateDepartmentEntity(CreateDepartmentRecord request, int companyId)
    {
        return new Department
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            EmpresaID = companyId
        };
    }

    private async Task SaveDepartmentAsync(Department department, CancellationToken cancellationToken)
    {
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IResult GenerateSuccessResponse(int departmentId)
    {
        return Results.Created($"/departments/{departmentId}", new 
        { 
            Message = "Departamento creado con éxito.", 
            DepartmentId = departmentId 
        });
    }
}