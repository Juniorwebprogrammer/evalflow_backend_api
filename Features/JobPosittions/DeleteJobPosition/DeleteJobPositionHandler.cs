using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.JobPosittions.DeleteJobPosition;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.JobPositions.ManageJobPositions;

public class DeleteJobPositionHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<DeleteJobPositionRecord, IResult>
{
    public async Task<IResult> Handle(DeleteJobPositionRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) return Results.Unauthorized();

        var position = await GetJobPositionWithUsersAsync(request.Id, company.Id, cancellationToken);
        if (position is null) return Results.NotFound("Cargo no encontrado.");

        await DeleteAndSaveJobPositionAsync(position, cancellationToken);

        return Results.Ok(new { Message = "Cargo eliminado correctamente. Los empleados asociados han quedado sin cargo asignado." });
    }

    private async Task<Company?> GetCompanyAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Companies
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<JobPosition?> GetJobPositionWithUsersAsync(int id, int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.JobPositions
            .Include(jp => jp.Usuarios)
            .FirstOrDefaultAsync(jp => jp.Id == id && jp.EmpresaID == companyId, cancellationToken);
    }

    private async Task DeleteAndSaveJobPositionAsync(JobPosition position, CancellationToken cancellationToken)
    {
        foreach (var user in position.Usuarios)
        {
            user.CargoId = null;
        }

        dbContext.JobPositions.Remove(position);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}