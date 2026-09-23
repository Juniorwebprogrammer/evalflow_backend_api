using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Team.AssignDepartment;

public class AssignDepartmentHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<AssignDepartmentRecord, IResult>
{
    public async Task<IResult> Handle(AssignDepartmentRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) 
        {
            return Results.Unauthorized();
        }

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) 
        {
            return Results.Unauthorized();
        }

        var employee = await GetEmployeeAsync(request.UserId, company.Id, cancellationToken);
        if (employee is null) 
        {
            return Results.NotFound(new { Message = "Empleado no encontrado en esta empresa." });
        }

        var validationError = await ValidateDepartmentAsync(request.DepartmentId, company.Id, cancellationToken);
        if (validationError is not null) 
        {
            return validationError;
        }

        await AssignDepartmentAndSaveAsync(employee, request.DepartmentId, cancellationToken);

        return GenerateSuccessResponse(request.DepartmentId);
    }

    private async Task<Company?> GetCompanyAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Companies
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<User?> GetEmployeeAsync(int userId, int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.EmpresaID == companyId, cancellationToken);
    }

    private async Task<IResult?> ValidateDepartmentAsync(int? departmentId, int companyId, CancellationToken cancellationToken)
    {
        if (!departmentId.HasValue) return null; 

        var departmentExists = await dbContext.Departments
            .AnyAsync(d => d.Id == departmentId.Value && d.EmpresaID == companyId, cancellationToken);
        
        return departmentExists 
            ? null 
            : Results.BadRequest(new { Message = "El departamento no existe o no pertenece a tu empresa." });
    }

    private async Task AssignDepartmentAndSaveAsync(User employee, int? departmentId, CancellationToken cancellationToken)
    {
        employee.DepartamentoId = departmentId;
        employee.FechaActualizacion = DateTime.UtcNow;

        dbContext.Users.Update(employee);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IResult GenerateSuccessResponse(int? departmentId)
    {
        var action = departmentId.HasValue ? "asignado al" : "removido del";
        return Results.Ok(new { Message = $"Usuario {action} departamento correctamente." });
    }
}