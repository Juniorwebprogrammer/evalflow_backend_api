using System.Security.Claims;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Infrastructure.Plans;

/// <summary>
/// A company without a plan (none in Vexxen: never bought, trial over, canceled or unpaid) is read-only: its members
/// can still sign in and see evaluations and results, but every write answers 403 PLAN_INACTIVE. Sessions,
/// onboarding, the profile, deleting the company and the Administrator stay available. The Admin API (/admin/v1) is
/// out of its scope: its caller is Vexxen Administrator, not a member of a company.
/// </summary>
public class PlanAccessMiddleware(RequestDelegate next)
{
    public const string PlanInactiveCode = "PLAN_INACTIVE";

    private static readonly string[] ExemptPrefixes = ["/Auth", "/Onboarding", "/Profile", "/Company/delete", "/hubs", "/admin/v1"];

    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (!IsWrite(context.Request.Method)
            || context.User.Identity?.IsAuthenticated != true
            || context.User.IsInRole(AppRoles.Administrator)
            || ExemptPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var tenantId = context.User.FindFirstValue("identificationId");
        var status = tenantId is null
            ? null
            : await dbContext.Companies.AsNoTracking()
                .Where(c => c.IdentificationId == tenantId)
                .Select(c => c.PlanStatus)
                .FirstOrDefaultAsync(context.RequestAborted);

        // Unknown company: let the endpoint answer as it always did.
        if (status is null || PlanStatuses.HasAccess(status))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            Code = PlanInactiveCode,
            Message = "Tu empresa no tiene un plan activo de EvalFlow: puedes consultar la información, pero no hacer cambios. Elige un plan en Vexxen para continuar.",
        }, context.RequestAborted);
    }

    private static bool IsWrite(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
}
