using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationCycles.GetAllEvaluationCycles;

public class GetAllEvaluationCyclesHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetAllEvaluationCyclesRecord, IResult>
{
    public async Task<IResult> Handle(GetAllEvaluationCyclesRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var companyId = await GetCompanyIdAsync(tenantId, cancellationToken);
        if (companyId is null) return Results.Unauthorized();

        var cycles = await FetchCyclesAsync(companyId.Value, cancellationToken);

        return Results.Ok(cycles);
    }

    private async Task<int?> GetCompanyIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
            
        return company?.Id;
    }

    private async Task<List<EvaluationCycleSummaryDto>> FetchCyclesAsync(int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationCycles
            .AsNoTracking()
            .Where(c => c.EmpresaID == companyId)
            .OrderByDescending(c => c.FechaInicio)
            .Select(c => new EvaluationCycleSummaryDto(
                c.Id,
                c.Nombre,
                c.Activo,
                c.FechaInicio,
                c.FechaFin,
                c.Templates.Count(),
                c.Templates.Select(t => t.Id).ToList(),
                c.TipoEvaluación,
                c.FechaCompletado
            ))
            .ToListAsync(cancellationToken);
    }
}