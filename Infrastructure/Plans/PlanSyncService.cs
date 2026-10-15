using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Vexxen;

namespace evalflow_backend_api.Infrastructure.Plans;

/// <summary>
/// Keeps a company's plan in step with the Vexxen subscription of its billing user (the owner who created it).
/// Runs on sign-in, on session refresh and when the company is created.
/// </summary>
public interface IPlanSyncService
{
    /// <summary>
    /// Reads the plan from Vexxen and saves it. Skipped for manual plans (Enterprise), for companies without a
    /// billing user, and when it was read less than <see cref="PlanSyncService.MaxAge"/> ago (unless forced). If
    /// Vexxen is down the company keeps its last known plan.
    /// </summary>
    Task SyncAsync(Company company, CancellationToken cancellationToken, bool force = false);
}

public class PlanSyncService(AppDbContext dbContext, IVexxenClient vexxen, ILogger<PlanSyncService> logger) : IPlanSyncService
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    public async Task SyncAsync(Company company, CancellationToken cancellationToken, bool force = false)
    {
        if (company.PlanManual || string.IsNullOrWhiteSpace(company.BillingVexxenUserId)) return;

        var now = DateTime.UtcNow;
        if (!force && company.PlanSyncedAt is { } syncedAt && now - syncedAt < MaxAge) return;

        try
        {
            var access = await vexxen.GetAccessAsync(company.BillingVexxenUserId, cancellationToken);
            PlanSync.Apply(company, access, now);
        }
        catch (VexxenUserNotFoundException)
        {
            // The billing account was deleted in Vexxen: its subscription is gone with it.
            PlanSync.ApplyNoAccess(company, now);
        }
        catch (VexxenUnavailableException ex)
        {
            logger.LogWarning(ex, "Couldn't sync the plan of company {CompanyId}; keeping {Status}", company.Id, company.PlanStatus);
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>How a Vexxen access becomes the company's plan. Pure, so it's easy to test.</summary>
public static class PlanSync
{
    public static void Apply(Company company, VexxenAccess access, DateTime now)
    {
        var status = PlanStatuses.FromVexxen(access.Access ? access.Status : PlanStatuses.None);
        var planId = PlanCatalog.FromVexxenPlanId(access.Plan?.Id);

        // A plan EvalFlow doesn't sell (or no plan at all) gives no access. Without access PlanId stays as it was:
        // the limits don't matter while the company is read-only, and its data is never touched.
        if (!PlanStatuses.HasAccess(status) || planId is null)
        {
            ApplyNoAccess(company, now);
            return;
        }

        company.PlanId = planId.Value;
        company.PlanStatus = status;
        company.PlanAccessEndsAt = access.AccessEndsAt;
        company.TrialEndsAt = access.TrialEndsAt;
        company.PlanSyncedAt = now;
    }

    public static void ApplyNoAccess(Company company, DateTime now)
    {
        company.PlanStatus = PlanStatuses.None;
        company.PlanAccessEndsAt = null;
        company.TrialEndsAt = null;
        company.PlanSyncedAt = now;
    }
}
