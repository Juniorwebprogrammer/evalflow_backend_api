using System.Text.Json;
using evalflow_backend_api.Features.AdminApi.Access;
using evalflow_backend_api.Features.AdminApi.Contract;
using evalflow_backend_api.Features.AdminApi.Http;
using evalflow_backend_api.Features.AdminApi.Resources;
using MediatR;

namespace evalflow_backend_api.Features.AdminApi;

/// <summary>
/// The Admin API for Vexxen Administrator (contract v1.2, docs/admin-contract-v1.md in the vexxen-administrator
/// repo): manifest, health, stats, the list and detail of each resource, their actions and their edit. Only the VexxenAdmin
/// scheme authenticates here (no X-Api-Key, no EvalFlow JWT, no CORS: the caller is the desktop app's main process).
/// </summary>
public static class AdminApiEndpoints
{
    public const string BasePath = "/admin/v1";

    public static IServiceCollection AddAdminApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddVexxenAdminAuthentication(configuration);
        services.AddScoped<AdminAppInfoProvider>();
        // Manifest order.
        services.AddScoped<IAdminResource, CompaniesAdminResource>();
        services.AddScoped<IAdminResource, UsersAdminResource>();
        services.AddScoped<IAdminResource, EvaluationCyclesAdminResource>();
        services.AddScoped<IAdminResource, EmailOutboxAdminResource>();
        services.AddScoped<AdminResourceCatalog>();
        return services;
    }

    /// <summary>Before authentication: X-Request-Id, no-store and problem+json for every /admin/v1 response.</summary>
    public static IApplicationBuilder UseAdminApi(this IApplicationBuilder app) =>
        app.UseWhen(context => context.Request.Path.StartsWithSegments(BasePath), branch => branch.UseMiddleware<AdminApiMiddleware>());

    public static void MapAdminApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(BasePath)
            .WithTags("AdminApi")
            .RequireAuthorization(VexxenAdminAuthentication.Policy)
            .AddEndpointFilter<AdminApiAccessFilter>();

        group.MapGet("/manifest", (HttpContext context, ISender sender) =>
            sender.Send(new GetAdminManifestRecord(AdminCaller.From(context).CanWrite)));

        group.MapGet("/health", (ISender sender) => sender.Send(new GetAdminHealthRecord()));

        group.MapGet("/stats", (ISender sender) => sender.Send(new GetAdminStatsRecord()));

        group.MapGet("/{resource}", (string resource, HttpContext context, ISender sender) =>
            sender.Send(new ListAdminResourceRecord(resource, Flatten(context.Request.Query))));

        group.MapGet("/{resource}/{id}", (string resource, string id, ISender sender) =>
            sender.Send(new GetAdminResourceItemRecord(resource, id)));

        group.MapPatch("/{resource}/{id}", async (string resource, string id, HttpContext context, ISender sender) =>
            await sender.Send(new EditAdminResourceItemRecord(resource, id, await ReadBodyAsync(context.Request),
                AdminCaller.From(context), AdminRequestId.Get(context))));

        group.MapPost("/{resource}/{id}/actions/{action}", async (string resource, string id, string action, HttpContext context, ISender sender) =>
            await sender.Send(new RunAdminActionRecord(resource, id, action, await ReadBodyAsync(context.Request),
                AdminCaller.From(context), AdminRequestId.Get(context))));
    }

    private static List<KeyValuePair<string, string>> Flatten(IQueryCollection query) =>
        query.SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string>(pair.Key, value ?? ""))).ToList();

    private const int MaxBodyBytes = 64 * 1024;

    // Read by hand so a malformed body is a 400 invalid_input (contract §12.3, §13.2), not the framework's own 400.
    private static async Task<JsonElement?> ReadBodyAsync(HttpRequest request)
    {
        if (request.ContentLength > MaxBodyBytes) throw TooLarge();

        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, request.HttpContext.RequestAborted)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes) throw TooLarge();
            buffer.Write(chunk, 0, read);
        }
        if (buffer.Length == 0) return null;

        try
        {
            using var document = JsonDocument.Parse(buffer.ToArray());
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw AdminApiException.InvalidInput("The body isn't valid JSON", [new("body", "Invalid JSON")]);
        }

        static AdminApiException TooLarge() =>
            AdminApiException.InvalidInput($"The body is larger than {MaxBodyBytes / 1024} KB", [new("body", "Too large")]);
    }
}

/// <summary>
/// After the VexxenAdmin scheme has validated the token: the 403 rules (scope admin:read, some grant, no tenant;
/// contract §5.2), the audit log of every request (sub, email, path, query) and the Admin API errors as problem+json.
/// </summary>
public class AdminApiAccessFilter(ILogger<AdminApiAccessFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var requestId = AdminRequestId.Get(http);
        var caller = AdminCaller.FromPrincipal(http.User);

        var access = caller.CheckRead();
        if (!access.Allowed)
        {
            logger.LogWarning("[admin-api] {RequestId} sub={Sub} email={Email} {Method} {Path} denied: {Code}",
                requestId, caller.Sub, caller.Email, http.Request.Method, http.Request.Path.Value, access.Code);
            return AdminProblem.Result(http, StatusCodes.Status403Forbidden, access.Code!, access.Detail);
        }

        http.Items[AdminCaller.ItemKey] = caller;
        logger.LogInformation("[admin-api] {RequestId} sub={Sub} email={Email} {Method} {Path}{Query}",
            requestId, caller.Sub, caller.Email, http.Request.Method, http.Request.Path.Value, http.Request.QueryString.Value);

        try
        {
            return await next(context);
        }
        catch (AdminApiException ex)
        {
            return AdminProblem.From(http, ex);
        }
    }
}
