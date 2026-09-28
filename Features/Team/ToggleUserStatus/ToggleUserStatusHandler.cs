using evalflow_backend_api.Infrastructure.Plans;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Team.ToggleUserStatus;

public class ToggleUserStatusHandler(AppDbContext dbContext, ICurrentUserService currentUser, IPlanLimitService planLimits) : IRequestHandler<ToggleUserStatusRecord, IResult>
{
    public async Task<IResult> Handle(ToggleUserStatusRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) return Results.Unauthorized();

        var employee = await GetEmployeeAsync(request.UserId, company.Id, cancellationToken);
        if (employee is null) return Results.NotFound("Empleado no encontrado.");

        var limitError = await planLimits.RunExclusiveAsync(company.Id, async ct =>
        {
            // Reactivating an account takes a seat again; deactivating is always allowed.
            if (request.Activo && !employee.Activo)
            {
                var error = await planLimits.CheckAsync(company.Id, PlanLimit.Employees, ct);
                if (error is not null) return error;
            }
            await UpdateAndSaveStatusAsync(employee, request.Activo, ct);
            return null;
        }, cancellationToken);
        if (limitError is not null) return limitError;

        var statusMessage = request.Activo ? "activada" : "desactivada";
        return Results.Ok(new { Message = $"La cuenta del empleado ha sido {statusMessage} correctamente." });
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

    private async Task UpdateAndSaveStatusAsync(User employee, bool isActivo, CancellationToken cancellationToken)
    {
        employee.Activo = isActivo;
        employee.FechaActualizacion = DateTime.UtcNow;

        dbContext.Users.Update(employee);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}