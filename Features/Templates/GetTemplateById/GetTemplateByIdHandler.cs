using evalflow_backend_api.Features.Templates.TemplatesDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Templates.GetTemplateById;

public class GetTemplateByIdHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetTemplateByIdRecord, IResult>
{
    public async Task<IResult> Handle(GetTemplateByIdRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        var company = await dbContext.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
        if (company is null) return Results.Unauthorized();

        var template = await FetchTemplateDetailsAsync(request.Id, company.Id, cancellationToken);
        
        if (template is null) return Results.NotFound(new { Message = "Plantilla no encontrada." });

        return Results.Ok(template);
    }

    private async Task<TemplateDetailsDto?> FetchTemplateDetailsAsync(int templateId, int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.Templates
            .AsNoTracking()
            .Where(t => t.Id == templateId && t.EmpresaID == companyId)
            .Select(t => new TemplateDetailsDto(
                t.Id,
                t.Titulo,
                t.Descripcion,
                t.FechaInicio,
                t.FechaFin,
                t.UsuariosAsignados.Select(u => u.Id).ToList())
            )
            .FirstOrDefaultAsync(cancellationToken);
    }
}