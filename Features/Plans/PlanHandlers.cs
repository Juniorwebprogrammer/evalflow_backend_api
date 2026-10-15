using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Plans;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Vexxen;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Features.Plans;

/// <summary>Every plan on sale, e.g. for the sign-up plan picker.</summary>
public class GetPlansHandler(AppDbContext dbContext) : IRequestHandler<GetPlansRecord, IResult>
{
    public async Task<IResult> Handle(GetPlansRecord request, CancellationToken cancellationToken)
    {
        var plans = await dbContext.Plans.AsNoTracking().OrderBy(p => p.Id).ToListAsync(cancellationToken);
        var source = plans.Count > 0 ? plans : PlanCatalog.All.ToList();
        return Results.Ok(source.Select(PlanDto.From).ToList());
    }
}

public class GetCompanyPlanHandler(AppDbContext dbContext, ICurrentUserService currentUser, IPlanLimitService planLimits,
    IOptions<VexxenSettings> vexxenSettings)
    : IRequestHandler<GetCompanyPlanRecord, IResult>
{
    public async Task<IResult> Handle(GetCompanyPlanRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var company = await dbContext.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
        if (company is null) return Results.Unauthorized();

        var plan = await planLimits.GetPlanAsync(company.Id, cancellationToken);
        var usage = await planLimits.GetUsageAsync(company.Id, cancellationToken);

        return Results.Ok(new CompanyPlanDto(
            PlanDto.From(plan),
            new PlanUsageDto(usage.Employees, usage.ActiveCycles, usage.CyclesThisYear, usage.CustomTemplates, usage.Departments,
                usage.AiAnalysesThisMonth),
            new PlanStatusDto(company.PlanStatus, PlanStatuses.HasAccess(company.PlanStatus), company.PlanAccessEndsAt,
                company.TrialEndsAt, company.PlanManual, vexxenSettings.Value.CatalogUrl)));
    }
}

public class ChangeCompanyPlanHandler(AppDbContext dbContext, IPlanSyncService planSync) : IRequestHandler<ChangeCompanyPlanRecord, IResult>
{
    public async Task<IResult> Handle(ChangeCompanyPlanRecord request, CancellationToken cancellationToken)
    {
        if (request.PlanId is { } planId && !PlanCatalog.Exists(planId))
            return Results.BadRequest(new { Message = "El plan indicado no existe." });

        var company = await dbContext.Companies
            .FirstOrDefaultAsync(c => c.IdentificationId == request.IdentificationId, cancellationToken);
        if (company is null) return Results.NotFound(new { Message = "Empresa no encontrada." });

        company.FechaActualizacion = DateTime.UtcNow;
        if (request.PlanId is { } manualPlanId)
        {
            // Downgrades keep existing data: the company is only blocked from adding more.
            company.PlanId = manualPlanId;
            company.PlanManual = true;
            company.PlanStatus = PlanStatuses.Active;
            company.PlanAccessEndsAt = null;
            company.TrialEndsAt = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Back to the Vexxen subscription of its billing user, read right now.
            company.PlanManual = false;
            await dbContext.SaveChangesAsync(cancellationToken);
            await planSync.SyncAsync(company, cancellationToken, force: true);
        }

        return Results.Ok(new { Message = "Plan actualizado correctamente.", company.PlanId, company.PlanStatus, company.PlanManual });
    }
}
