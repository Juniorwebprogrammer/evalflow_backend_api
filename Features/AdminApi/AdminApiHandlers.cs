using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.Admin.CreateAdministrator;
using evalflow_backend_api.Features.AdminApi.Access;
using evalflow_backend_api.Features.AdminApi.Contract;
using evalflow_backend_api.Features.AdminApi.Resources;
using evalflow_backend_api.Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Features.AdminApi;

/// <summary>What the manifest and /health say about this app: id evalflow, its version and its environment.</summary>
public class AdminAppInfoProvider(IOptions<VexxenAdminSettings> settings, IHostEnvironment environment)
{
    public const string AppId = "evalflow";
    public const string AppName = "EvalFlow";

    private static readonly string AssemblyVersion = ReadVersion();

    public AdminAppInfo Get() =>
        new(AppId, AppName, AssemblyVersion, VexxenAdminAuthentication.EnvironmentName(settings.Value, environment));

    public string Version => AssemblyVersion;

    // APP_VERSION if the deploy sets it; if not, the assembly's (with the short commit when the build knows it).
    private static string ReadVersion()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("APP_VERSION");
        if (!string.IsNullOrWhiteSpace(fromEnvironment)) return fromEnvironment.Trim();

        var informational = typeof(AdminAppInfoProvider).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational)) return "unknown";
        var plus = informational.IndexOf('+');
        return plus < 0 ? informational : informational[..Math.Min(informational.Length, plus + 8)];
    }
}

public class GetAdminManifestHandler(AdminResourceCatalog catalog, AdminAppInfoProvider appInfo) : IRequestHandler<GetAdminManifestRecord, IResult>
{
    public Task<IResult> Handle(GetAdminManifestRecord request, CancellationToken cancellationToken)
    {
        var resources = catalog.All
            .Select(r => request.CanWrite ? r.Descriptor : r.Descriptor with { Actions = [] })
            .ToList();
        return Task.FromResult(Results.Ok(new AdminManifest(AdminContract.ContractVersion, appInfo.Get(), resources)));
    }
}

public class GetAdminHealthHandler(AppDbContext dbContext, AdminAppInfoProvider appInfo, ILogger<GetAdminHealthHandler> logger)
    : IRequestHandler<GetAdminHealthRecord, IResult>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<IResult> Handle(GetAdminHealthRecord request, CancellationToken cancellationToken)
    {
        var database = await CheckDatabaseAsync(cancellationToken);
        var checks = new[] { database };
        var status = checks.Any(c => c.Status == "down") ? "down" : checks.Any(c => c.Status == "degraded") ? "degraded" : "ok";
        return Results.Ok(new AdminHealth(status, appInfo.Version, AdminFormat.DateTime(DateTime.UtcNow), checks));
    }

    // Never a 500: if the database doesn't answer, the status says so.
    private async Task<HealthCheck> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var ok = await dbContext.Database.CanConnectAsync(timeout.Token);
            var latency = (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            return ok ? new HealthCheck("database", "ok", latency) : new HealthCheck("database", "down", latency, "Can't connect to the database");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "[admin-api] health: the database check failed");
            return new HealthCheck("database", "down", (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                timeout.IsCancellationRequested ? "Timed out" : "Can't connect to the database");
        }
    }
}

/// <summary>
/// The home panel: companies (total, by plan among those with access, and without plan), users, active cycles,
/// evaluations submitted in 30 days and the email queue. The internal company of the administrators doesn't count.
/// </summary>
public class GetAdminStatsHandler(AppDbContext dbContext) : IRequestHandler<GetAdminStatsRecord, IResult>
{
    private static readonly string[] WithAccess = [PlanStatuses.Trialing, PlanStatuses.Active, PlanStatuses.Canceling, PlanStatuses.PastDue];

    public async Task<IResult> Handle(GetAdminStatsRecord request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var since = now.AddDays(-30);
        var companies = CompaniesAdminResource.Visible(dbContext);

        var companiesTotal = await companies.CountAsync(cancellationToken);
        var byPlan = await companies
            .Where(c => WithAccess.Contains(c.PlanStatus))
            .GroupBy(c => c.PlanId)
            .Select(g => new { PlanId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var withoutPlan = companiesTotal - byPlan.Sum(p => p.Count);

        var usersTotal = await UsersAdminResource.Visible(dbContext).CountAsync(cancellationToken);
        var activeCycles = await EvaluationCyclesAdminResource.Visible(dbContext)
            .CountAsync(c => c.Activo && c.FechaCompletado == null, cancellationToken);
        var evaluationsCompleted = await dbContext.EvaluationSubmissions.AsNoTracking()
            .Where(s => s.IsCompleted && s.SubmittedAt >= since)
            .Where(s => s.Cycle!.Empresa!.IdentificationId != CreateAdministratorCommand.InternalCompanyId)
            .CountAsync(cancellationToken);
        var emails = await dbContext.EmailOutboxMessages.AsNoTracking()
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        int Emails(Domain.Enums.EmailOutboxStatus status) => emails.FirstOrDefault(e => e.Status == status)?.Count ?? 0;

        var cards = new List<StatCard> { new("companies_total", "Empresas", companiesTotal, "number") };
        cards.AddRange(PlanCatalog.All.Select(plan => new StatCard($"companies_plan_{plan.Code}", $"Empresas {plan.Nombre}",
            byPlan.FirstOrDefault(p => p.PlanId == plan.Id)?.Count ?? 0, "number") { Hint = "Con el plan en vigor (en prueba, activo, cancelándose o con cobro pendiente)" }));
        cards.Add(new StatCard("companies_without_plan", "Empresas sin plan", withoutPlan, "number") { Hint = "En solo lectura" });
        cards.Add(new StatCard("users_total", "Usuarios", usersTotal, "number"));
        cards.Add(new StatCard("cycles_active", "Ciclos activos", activeCycles, "number"));
        cards.Add(new StatCard("evaluations_completed_30d", "Evaluaciones completadas (30 días)", evaluationsCompleted, "number"));
        cards.Add(new StatCard("emails_failed", "Emails fallidos", Emails(Domain.Enums.EmailOutboxStatus.Fallido), "number"));
        cards.Add(new StatCard("emails_pending", "Emails pendientes", Emails(Domain.Enums.EmailOutboxStatus.Pendiente), "number"));

        return Results.Ok(new AdminStats(AdminFormat.DateTime(now), cards));
    }
}

public class ListAdminResourceHandler(AdminResourceCatalog catalog) : IRequestHandler<ListAdminResourceRecord, IResult>
{
    public async Task<IResult> Handle(ListAdminResourceRecord request, CancellationToken cancellationToken)
    {
        var resource = catalog.Require(request.Resource);
        var query = AdminListQuery.Parse(resource.Descriptor, request.Query);
        var result = await resource.ListAsync(query, cancellationToken);
        return Results.Ok(new AdminListResponse(result.Items, query.Page, query.PageSize, result.Total));
    }
}

public class GetAdminResourceItemHandler(AdminResourceCatalog catalog) : IRequestHandler<GetAdminResourceItemRecord, IResult>
{
    public async Task<IResult> Handle(GetAdminResourceItemRecord request, CancellationToken cancellationToken)
    {
        var resource = catalog.Require(request.Resource);
        if (!resource.Descriptor.Detail) throw AdminApiException.NotFound($"{request.Resource} has no detail");
        var item = await resource.GetAsync(request.Id, cancellationToken)
                   ?? throw AdminApiException.NotFound($"{request.Resource}/{request.Id} doesn't exist");
        return Results.Ok(new AdminItemResponse(item));
    }
}

/// <summary>
/// Runs an action (contract §12.3): admin:write and an operator grant (403), resource, action and record (404), the
/// inputs (400 invalid_input), appliesTo (409). Every attempt goes to the log with who, what and the inputs.
/// </summary>
public class RunAdminActionHandler(AdminResourceCatalog catalog, ILogger<RunAdminActionHandler> logger) : IRequestHandler<RunAdminActionRecord, IResult>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IResult> Handle(RunAdminActionRecord request, CancellationToken cancellationToken)
    {
        var caller = request.Caller;
        var access = caller.CheckWrite();
        if (!access.Allowed) throw new AdminApiException(StatusCodes.Status403Forbidden, access.Code!, access.Detail!);

        var resource = catalog.Require(request.Resource);
        var action = resource.Descriptor.Actions.FirstOrDefault(a => a.Key == request.Action)
                     ?? throw AdminApiException.NotFound($"{request.Resource} has no action {request.Action}");

        var inputs = AdminActionInputs.Validate(action, request.Body);
        var current = await resource.GetAsync(request.Id, cancellationToken)
                      ?? throw AdminApiException.NotFound($"{request.Resource}/{request.Id} doesn't exist");
        if (action.AppliesTo is { } appliesTo && !Applies(current, appliesTo))
            throw AdminApiException.Conflict($"{action.Key} doesn't apply to {request.Resource}/{request.Id} in its current state");

        var inputsJson = JsonSerializer.Serialize(inputs, Json);
        logger.LogInformation("[admin-api] action {RequestId} sub={Sub} email={Email} {Resource}/{Id} {Action} inputs={Inputs}",
            request.RequestId, caller.Sub, caller.Email, request.Resource, request.Id, action.Key, inputsJson);

        try
        {
            var item = await resource.RunActionAsync(action.Key, request.Id, inputs, cancellationToken);
            logger.LogInformation("[admin-api] action {RequestId} {Resource}/{Id} {Action} done", request.RequestId, request.Resource, request.Id, action.Key);
            return Results.Ok(new AdminItemResponse(item));
        }
        catch (AdminApiException ex)
        {
            logger.LogWarning("[admin-api] action {RequestId} {Resource}/{Id} {Action} rejected: {Code} {Detail}",
                request.RequestId, request.Resource, request.Id, action.Key, ex.Code, ex.Message);
            throw;
        }
    }

    private static bool Applies(object item, ActionAppliesTo appliesTo)
    {
        var json = JsonSerializer.SerializeToElement(item, item.GetType(), Json);
        if (!json.TryGetProperty(appliesTo.Field, out var value)) return false;
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => value.GetRawText(),
        };
        return text is not null && appliesTo.Values.Contains(text);
    }
}
