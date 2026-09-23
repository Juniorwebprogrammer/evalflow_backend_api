using evalflow_backend_api.Features.Templates.TemplatesDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Templates.GetAllTemplates;

public class GetAllTemplatesHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetAllTemplatesRecord, IResult>
{
    public async Task<IResult> Handle(GetAllTemplatesRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var companyId = await GetCompanyIdAsync(tenantId, cancellationToken);
        if (companyId is null) return Results.Unauthorized();

        var templates = await FetchTemplatesSummaryAsync(companyId.Value, cancellationToken);

        return Results.Ok(templates);
    }

    private async Task<int?> GetCompanyIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
            
        return company?.Id;
    }

    private async Task<List<TemplateSummaryDto>> FetchTemplatesSummaryAsync(int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.Templates
            .AsNoTracking()
            .Where(t => t.EmpresaID == companyId)
            .OrderByDescending(t => t.FechaInicio)
            .Select(t => new TemplateSummaryDto(
                t.Id,
                t.Titulo,
                t.Descripcion,
                t.FechaInicio,
                t.FechaFin,
                t.UsuariosAsignados.Count(),
                t.UsuariosAsignados.Select(u => u.Id).ToList()
            ))
            .ToListAsync(cancellationToken);
    }
}