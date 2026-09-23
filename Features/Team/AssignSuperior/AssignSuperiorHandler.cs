using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Team.AssignSuperior;

public class AssignSuperiorHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<AssignSuperiorRecord, IResult>
{
    public async Task<IResult> Handle(AssignSuperiorRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) return Results.Unauthorized();
        if (request.UserId == request.SuperiorId)
            return Results.BadRequest(new { Message = "Un empleado no puede ser su propio superior." });

        var employee = await GetUserAsync(request.UserId, company.Id, cancellationToken);
        if (employee is null) return Results.NotFound(new { Message = "Empleado no encontrado." });

        if (request.SuperiorId.HasValue)
        {
            var superior = await GetUserAsync(request.SuperiorId.Value, company.Id, cancellationToken);
            if (superior is null) return Results.BadRequest(new { Message = "El superior no existe o pertenece a otra empresa." });

            var isCircular = await IsCircularReferenceAsync(request.UserId, request.SuperiorId.Value, cancellationToken);
            if (isCircular) return Results.BadRequest(new { Message = "No se puede asignar este superior porque crearía un bucle jerárquico infinito." });
        }

        await UpdateSuperiorAsync(employee, request.SuperiorId, cancellationToken);

        return Results.Ok(new { Message = "Superior asignado correctamente." });
    }

    private async Task<Company?> GetCompanyAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Companies.FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<User?> GetUserAsync(int userId, int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId && u.EmpresaID == companyId, cancellationToken);
    }

    private async Task<bool> IsCircularReferenceAsync(int employeeId, int newSuperiorId, CancellationToken cancellationToken)
    {
        int? currentSuperiorId = newSuperiorId;

        while (currentSuperiorId.HasValue)
        {
            if (currentSuperiorId.Value == employeeId) return true; // ¡Bucle detectado!

            var currentSuperior = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentSuperiorId.Value, cancellationToken);
            
            currentSuperiorId = currentSuperior?.SuperiorId;
        }
        return false;
    }

    private async Task UpdateSuperiorAsync(User employee, int? superiorId, CancellationToken cancellationToken)
    {
        employee.SuperiorId = superiorId;
        employee.FechaActualizacion = DateTime.UtcNow;
        dbContext.Users.Update(employee);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}