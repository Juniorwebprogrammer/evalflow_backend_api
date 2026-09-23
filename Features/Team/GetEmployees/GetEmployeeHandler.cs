using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Team.GetEmployees;

public class GetEmployeeHandler(AppDbContext dbContext, ICurrentUserService currentUserService) : IRequestHandler<GetEmployeeRecord, IResult>
{
    public async Task<IResult> Handle(GetEmployeeRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.GetIdentificationId();
        
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var employees = await FetchEmployeesByTenantAsync(tenantId, cancellationToken);
        
        return Results.Ok(employees);
    }

    private async Task<List<GetEmployeeResponse>> FetchEmployeesByTenantAsync(string tenantId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .Where(user => user.Empresa!.IdentificationId == tenantId)
            .OrderByDescending(user => user.FechaCreacion)
            .Select(user => new GetEmployeeResponse(
                user.Id.ToString(),
                user.Nombre,
                user.Apellidos,
                user.Email,
                user.Rol,
                user.Cargo!.Nombre,
                user.Activo,
                user.FechaCreacion
            ))
            .ToListAsync(cancellationToken);
    }
}