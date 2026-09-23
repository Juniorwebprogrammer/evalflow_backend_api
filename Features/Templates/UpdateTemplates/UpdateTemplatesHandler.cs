using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Templates.UpdateTemplates;

public class UpdateTemplateHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<UpdateTemplateRecord, IResult>
{
    public async Task<IResult> Handle(UpdateTemplateRecord request, CancellationToken cancellationToken)
    {
        if (request.FechaInicio >= request.FechaFin)
            return Results.BadRequest(new { Message = "La fecha de inicio debe ser anterior a la fecha de fin." });

        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) return Results.Unauthorized();

        var template = await GetTemplateWithUsersAsync(request.Id, company.Id, cancellationToken);
        if (template is null) return Results.NotFound(new { Message = "Plantilla no encontrada." });

        await UpdateAndSaveTemplateAsync(template, request, company.Id, cancellationToken);

        return Results.Ok(new { Message = "Plantilla actualizada correctamente." });
    }

    private async Task<Company?> GetCompanyAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Companies.FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<Template?> GetTemplateWithUsersAsync(int templateId, int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.Templates
            .Include(t => t.UsuariosAsignados)
            .FirstOrDefaultAsync(t => t.Id == templateId && t.EmpresaID == companyId, cancellationToken);
    }

    private async Task UpdateAndSaveTemplateAsync(Template template, UpdateTemplateRecord request, int companyId, CancellationToken cancellationToken)
    {
        template.Titulo = request.Titulo;
        template.Descripcion = request.Descripcion;
        template.FechaInicio = request.FechaInicio.ToUniversalTime();
        template.FechaFin = request.FechaFin.ToUniversalTime();

        var validUsers = await dbContext.Users
            .Where(u => u.EmpresaID == companyId && request.AssignedUserIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        template.UsuariosAsignados.Clear();
        foreach (var user in validUsers)
        {
            template.UsuariosAsignados.Add(user);
        }

        dbContext.Templates.Update(template);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}