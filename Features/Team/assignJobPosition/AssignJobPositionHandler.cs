using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Team.AssignJobPosition;

public class AssignJobPositionHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<AssignJobPositionRecord, IResult>
{
    public async Task<IResult> Handle(AssignJobPositionRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) return Results.Unauthorized();

        var employee = await GetEmployeeAsync(request.UserId, company.Id, cancellationToken);
        if (employee is null) return Results.NotFound("Empleado no encontrado.");

        if (request.JobPositionId.HasValue)
        {
            var isValidPosition = await ValidateJobPositionAsync(request.JobPositionId.Value, company.Id, cancellationToken);
            if (!isValidPosition) return Results.BadRequest("El cargo no existe o no pertenece a tu empresa.");
        }

        await UpdateAndSaveJobPositionAsync(employee, request.JobPositionId, cancellationToken);

        var action = request.JobPositionId.HasValue ? "asignado" : "removido";
        return Results.Ok(new { Message = $"Cargo {action} correctamente al empleado." });
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

    private async Task<bool> ValidateJobPositionAsync(int positionId, int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.JobPositions
            .AnyAsync(jp => jp.Id == positionId && jp.EmpresaID == companyId, cancellationToken);
    }

    private async Task UpdateAndSaveJobPositionAsync(User employee, int? jobPositionId, CancellationToken cancellationToken)
    {
        employee.CargoId = jobPositionId;
        employee.FechaActualizacion = DateTime.UtcNow;

        dbContext.Users.Update(employee);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}