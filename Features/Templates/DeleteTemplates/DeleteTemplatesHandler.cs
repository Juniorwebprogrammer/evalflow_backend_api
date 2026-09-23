using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Templates.DeleteTemplates;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Templates.DeleteTemplate;

public class DeleteTemplateHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<DeleteTemplateRecord, IResult>
{
    public async Task<IResult> Handle(DeleteTemplateRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) return Results.Unauthorized();

        var template = await GetTemplateAsync(request.Id, company.Id, cancellationToken);
        if (template is null) return Results.NotFound(new { Message = "Plantilla no encontrada." });

        // 🚀 TODO: VALIDACIÓN FUTURA (SISTEMA DE CICLOS)
        // Antes de eliminar, debemos verificar que esta plantilla no esté siendo usada en un ciclo activo.
        // 
        // var isInActiveCycle = await dbContext.Ciclos
        //    .AnyAsync(c => c.TemplateId == template.Id && c.Activo == true, cancellationToken);
        //
        // if (isInActiveCycle) 
        //    return Results.BadRequest(new { Message = "No puedes eliminar una plantilla que está siendo usada en un ciclo activo." });

        await DeleteAndSaveTemplateAsync(template, cancellationToken);

        return Results.Ok(new { Message = "Plantilla eliminada correctamente." });
    }

    private async Task<Company?> GetCompanyAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Companies.FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<Template?> GetTemplateAsync(int templateId, int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.Templates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.EmpresaID == companyId, cancellationToken);
    }

    private async Task DeleteAndSaveTemplateAsync(Template template, CancellationToken cancellationToken)
    {
        dbContext.Templates.Remove(template);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}