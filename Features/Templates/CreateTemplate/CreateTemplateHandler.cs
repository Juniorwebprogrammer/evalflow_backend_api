using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Templates.CreateTemplate;

public class CreateTemplateHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<CreateTemplateRecord, IResult>
{
    public async Task<IResult> Handle(CreateTemplateRecord request, CancellationToken cancellationToken)
    {
        if (request.FechaInicio >= request.FechaFin)
            return Results.BadRequest(new { Message = "La fecha de inicio debe ser anterior a la fecha de fin." });

        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) return Results.Unauthorized();

        var template = await BuildAndSaveTemplateAsync(request, company.Id, cancellationToken);

        return Results.Created($"/templates/{template.Id}", new { Message = "Plantilla creada con éxito.", Id = template.Id });
    }

    private async Task<Company?> GetCompanyAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Companies.FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<Template> BuildAndSaveTemplateAsync(CreateTemplateRecord request, int companyId, CancellationToken cancellationToken)
    {
        var validUsers = await dbContext.Users
            .Where(u => u.EmpresaID == companyId && request.AssignedUserIds.Contains(u.Id))
            .ToListAsync(cancellationToken);
        
        var template = new Template
        {
            Titulo = request.Titulo,
            Descripcion = request.Descripcion,
            FechaInicio = request.FechaInicio.ToUniversalTime(),
            FechaFin = request.FechaFin.ToUniversalTime(),
            EmpresaID = companyId,
            UsuariosAsignados = validUsers 
        };

        dbContext.Templates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);

        return template;
    }
}