using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Infrastructure.Plans;

/// <summary>A resource capped by the company's plan.</summary>
public enum PlanLimit
{
    Employees,
    ActiveCycles,
    CyclesPerYear,
    CustomTemplates,
    Departments,
}

/// <summary>A feature only some plans include.</summary>
public enum PlanFeature
{
    Ai,
}

/// <summary>Current consumption of every capped resource.</summary>
public record PlanUsage(int Employees, int ActiveCycles, int CyclesThisYear, int CustomTemplates, int Departments);

/// <summary>Body of the 403 returned when a plan limit blocks an action (stable <c>Code</c> for the frontend).</summary>
public record PlanLimitError(string Code, string Message, string Resource, int Limit, int Current, int PlanId, string PlanName);

public interface IPlanLimitService
{
    Task<Plan> GetPlanAsync(int companyId, CancellationToken cancellationToken);

    Task<PlanUsage> GetUsageAsync(int companyId, CancellationToken cancellationToken);

    /// <summary>
    /// Null when the company can add <paramref name="adding"/> more of <paramref name="limit"/>;
    /// otherwise a 403 <see cref="PlanLimitError"/> ready to return from a handler.
    /// <paramref name="year"/> selects the calendar year for <see cref="PlanLimit.CyclesPerYear"/>;
    /// <paramref name="excludeCycleId"/> leaves a cycle being edited out of the cycle counts.
    /// </summary>
    Task<IResult?> CheckAsync(int companyId, PlanLimit limit, CancellationToken cancellationToken,
        int adding = 1, int? year = null, int? excludeCycleId = null);

    Task<bool> HasFeatureAsync(int companyId, PlanFeature feature, CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="action"/> — typically <see cref="CheckAsync"/> followed by the
    /// insert/update it guards — inside a transaction holding a per-company lock, so two
    /// concurrent requests can't both pass the check and overshoot a limit. Returns what
    /// the action returns (a limit error, or null on success).
    /// </summary>
    Task<IResult?> RunExclusiveAsync(int companyId, Func<CancellationToken, Task<IResult?>> action,
        CancellationToken cancellationToken);
}

/// <summary>
/// Enforces plan limits. Downgrades never delete or deactivate anything: a
/// company over a limit keeps what it has and is only blocked from adding more.
/// </summary>
public class PlanLimitService(AppDbContext dbContext) : IPlanLimitService
{
    public const string LimitReachedCode = "PLAN_LIMIT_REACHED";

    public async Task<Plan> GetPlanAsync(int companyId, CancellationToken cancellationToken)
    {
        var planId = await dbContext.Companies
            .Where(c => c.Id == companyId)
            .Select(c => c.PlanId)
            .FirstOrDefaultAsync(cancellationToken);

        var plan = await dbContext.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);
        return plan ?? PlanCatalog.Get(planId);
    }

    public async Task<PlanUsage> GetUsageAsync(int companyId, CancellationToken cancellationToken) => new(
        await CountAsync(companyId, PlanLimit.Employees, null, null, cancellationToken),
        await CountAsync(companyId, PlanLimit.ActiveCycles, null, null, cancellationToken),
        await CountAsync(companyId, PlanLimit.CyclesPerYear, DateTime.UtcNow.Year, null, cancellationToken),
        await CountAsync(companyId, PlanLimit.CustomTemplates, null, null, cancellationToken),
        await CountAsync(companyId, PlanLimit.Departments, null, null, cancellationToken));

    public async Task<IResult?> CheckAsync(int companyId, PlanLimit limit, CancellationToken cancellationToken,
        int adding = 1, int? year = null, int? excludeCycleId = null)
    {
        var plan = await GetPlanAsync(companyId, cancellationToken);
        var max = MaxFor(plan, limit);
        if (max is null) return null;

        var current = await CountAsync(companyId, limit, year ?? DateTime.UtcNow.Year, excludeCycleId, cancellationToken);
        if (current + adding <= max.Value) return null;

        return Results.Json(
            new PlanLimitError(LimitReachedCode, MessageFor(limit, max.Value, plan.Nombre), limit.ToString(),
                max.Value, current, plan.Id, plan.Nombre),
            statusCode: StatusCodes.Status403Forbidden);
    }

    public async Task<bool> HasFeatureAsync(int companyId, PlanFeature feature, CancellationToken cancellationToken)
    {
        var plan = await GetPlanAsync(companyId, cancellationToken);
        return feature switch
        {
            PlanFeature.Ai => plan.HasAiFeatures,
            _ => false,
        };
    }

    /// <summary>First key of the two-int advisory lock, namespacing it to plan limits.</summary>
    private const int PlanLockNamespace = 7351;

    public async Task<IResult?> RunExclusiveAsync(int companyId, Func<CancellationToken, Task<IResult?>> action,
        CancellationToken cancellationToken)
    {
        // The in-memory provider (unit tests) has neither transactions nor advisory locks.
        if (!dbContext.Database.IsRelational() || dbContext.Database.CurrentTransaction is not null)
            return await action(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Released automatically at commit/rollback; concurrent callers for the same
        // company wait here until the first one has saved (or bailed out).
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({PlanLockNamespace}, {companyId})", cancellationToken);

        var result = await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public static int? MaxFor(Plan plan, PlanLimit limit) => limit switch
    {
        PlanLimit.Employees => plan.MaxEmployees,
        PlanLimit.ActiveCycles => plan.MaxActiveCycles,
        PlanLimit.CyclesPerYear => plan.MaxCyclesPerYear,
        PlanLimit.CustomTemplates => plan.MaxCustomTemplates,
        PlanLimit.Departments => plan.MaxDepartments,
        _ => null,
    };

    private Task<int> CountAsync(int companyId, PlanLimit limit, int? year, int? excludeCycleId, CancellationToken cancellationToken)
    {
        var cycles = dbContext.EvaluationCycles.Where(c => c.EmpresaID == companyId && c.Id != (excludeCycleId ?? 0));

        return limit switch
        {
            // Invited accounts are created active, so this already includes pending invitations.
            PlanLimit.Employees => dbContext.Users.CountAsync(u => u.EmpresaID == companyId && u.Activo, cancellationToken),
            PlanLimit.ActiveCycles => cycles.CountAsync(c => c.Activo && c.FechaCompletado == null, cancellationToken),
            PlanLimit.CyclesPerYear => cycles.CountAsync(c => c.FechaInicio.Year == (year ?? DateTime.UtcNow.Year), cancellationToken),
            PlanLimit.CustomTemplates => dbContext.Templates.CountAsync(t => t.EmpresaID == companyId && !t.IsDefault, cancellationToken),
            PlanLimit.Departments => dbContext.Departments.CountAsync(d => d.EmpresaID == companyId, cancellationToken),
            _ => Task.FromResult(0),
        };
    }

    private static string MessageFor(PlanLimit limit, int max, string planName)
    {
        var what = limit switch
        {
            PlanLimit.Employees => $"{max} empleados activos",
            PlanLimit.ActiveCycles => max == 1 ? "1 ciclo activo a la vez" : $"{max} ciclos activos a la vez",
            PlanLimit.CyclesPerYear => $"{max} ciclos de evaluación por año",
            PlanLimit.CustomTemplates => $"{max} plantillas propias",
            PlanLimit.Departments => $"{max} departamentos",
            _ => "este recurso",
        };
        return $"Tu plan {planName} permite hasta {what}. Mejora tu plan para añadir más.";
    }
}
