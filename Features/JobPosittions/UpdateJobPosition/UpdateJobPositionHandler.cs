using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.JobPosittions.UpdateJobPosition;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.JobPositions.ManageJobPositions;

public class UpdateJobPositionHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<UpdateJobPositionRecord, IResult>
{
    public async Task<IResult> Handle(UpdateJobPositionRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) return Results.Unauthorized();

        var position = await GetJobPositionAsync(request.Id, company.Id, cancellationToken);
        if (position is null) return Results.NotFound("Cargo no encontrado.");

        await UpdateAndSaveJobPositionAsync(position, request, cancellationToken);

        return Results.Ok(new { Message = "Cargo actualizado correctamente." });
    }

    private async Task<Company?> GetCompanyAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Companies
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<JobPosition?> GetJobPositionAsync(int id, int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.JobPositions
            .FirstOrDefaultAsync(jp => jp.Id == id && jp.EmpresaID == companyId, cancellationToken);
    }

    private async Task UpdateAndSaveJobPositionAsync(JobPosition position, UpdateJobPositionRecord request, CancellationToken cancellationToken)
    {
        position.Nombre = request.Nombre;
        position.Descripcion = request.Descripcion;

        dbContext.JobPositions.Update(position);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}