using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Plans;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

public class GetCompanyPlanHandler(AppDbContext dbContext, ICurrentUserService currentUser, IPlanLimitService planLimits)
    : IRequestHandler<GetCompanyPlanRecord, IResult>
{
    public async Task<IResult> Handle(GetCompanyPlanRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var companyId = await dbContext.Companies
            .Where(c => c.IdentificationId == tenantId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (companyId is null) return Results.Unauthorized();

        var plan = await planLimits.GetPlanAsync(companyId.Value, cancellationToken);
        var usage = await planLimits.GetUsageAsync(companyId.Value, cancellationToken);

        return Results.Ok(new CompanyPlanDto(
            PlanDto.From(plan),
            new PlanUsageDto(usage.Employees, usage.ActiveCycles, usage.CyclesThisYear, usage.CustomTemplates, usage.Departments)));
    }
}

public class ChangeCompanyPlanHandler(AppDbContext dbContext) : IRequestHandler<ChangeCompanyPlanRecord, IResult>
{
    public async Task<IResult> Handle(ChangeCompanyPlanRecord request, CancellationToken cancellationToken)
    {
        if (!PlanCatalog.Exists(request.PlanId))
            return Results.BadRequest(new { Message = "El plan indicado no existe." });

        var company = await dbContext.Companies
            .FirstOrDefaultAsync(c => c.IdentificationId == request.IdentificationId, cancellationToken);
        if (company is null) return Results.NotFound(new { Message = "Empresa no encontrada." });

        // Downgrades keep existing data: the company is only blocked from adding more.
        company.PlanId = request.PlanId;
        company.FechaActualizacion = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { Message = "Plan actualizado correctamente.", request.PlanId });
    }
}
