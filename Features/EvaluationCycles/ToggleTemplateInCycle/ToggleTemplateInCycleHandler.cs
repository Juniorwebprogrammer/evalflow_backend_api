using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationCycles.ToggleTemplateInCycle;

public class ToggleTemplateInCycleHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<ToggleTemplateInCycleRecord, IResult>
{
    public async Task<IResult> Handle(ToggleTemplateInCycleRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var cycle = await GetCycleWithTemplatesAsync(request.CycleId, tenantId, cancellationToken);
        if (cycle is null) return Results.NotFound(new { Message = "Ciclo no encontrado o no pertenece a tu empresa." });

        var template = await GetTemplateAsync(request.TemplateId, tenantId, cancellationToken);
        if (template is null) return Results.BadRequest(new { Message = "La plantilla no existe o no pertenece a tu empresa." });

        var actionMessage = await ToggleAndSaveAsync(cycle, template, cancellationToken);

        return Results.Ok(new { Message = actionMessage });
    }

    private async Task<EvaluationCycle?> GetCycleWithTemplatesAsync(int cycleId, string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationCycles
            .Include(c => c.Templates)
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.Empresa!.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<Template?> GetTemplateAsync(int templateId, string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Templates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.Empresa!.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<string> ToggleAndSaveAsync(EvaluationCycle cycle, Template template, CancellationToken cancellationToken)
    {
        var existsInCycle = cycle.Templates.Any(t => t.Id == template.Id);
        string actionMessage;

        if (existsInCycle)
        {
            cycle.Templates.Remove(template);
            actionMessage = "Plantilla removida del ciclo.";
        }
        else
        {
            cycle.Templates.Add(template);
            actionMessage = "Plantilla añadida al ciclo.";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return actionMessage;
    }
}