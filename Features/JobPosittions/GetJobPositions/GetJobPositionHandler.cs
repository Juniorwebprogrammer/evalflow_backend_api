using evalflow_backend_api.Features.JobPosittions.JobPositionDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.JobPositions.GetJobPositions;

public class GetJobPositionsHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetJobPositionsRecord, IResult>
{
    public async Task<IResult> Handle(GetJobPositionsRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var companyId = await GetCompanyIdAsync(tenantId, cancellationToken);
        if (companyId is null) return Results.Unauthorized();

        var positions = await FetchJobPositionsAsync(companyId.Value, cancellationToken);

        return Results.Ok(positions);
    }

    private async Task<int?> GetCompanyIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
            
        return company?.Id;
    }

    private async Task<List<JobPositionDto>> FetchJobPositionsAsync(int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.JobPositions
            .AsNoTracking()
            .Where(jp => jp.EmpresaID == companyId)
            .OrderBy(jp => jp.Nombre)
            .Select(jp => new JobPositionDto(
                jp.Id,
                jp.Nombre,
                jp.Descripcion,
                jp.Usuarios.Count()
            ))
            .ToListAsync(cancellationToken);
    }
}