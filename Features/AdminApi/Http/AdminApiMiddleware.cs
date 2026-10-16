using evalflow_backend_api.Features.AdminApi.Contract;

namespace evalflow_backend_api.Features.AdminApi.Http;

/// <summary>
/// Runs only for /admin/v1 (see <see cref="AdminApiEndpoints.UseAdminApi"/>), before authentication: every response
/// carries X-Request-Id and Cache-Control: no-store, an unexpected exception is a 500 internal without traces, and the
/// errors the framework answers without a body (unknown route 404, wrong method 405…) become problem+json too.
/// </summary>
public class AdminApiMiddleware(RequestDelegate next, ILogger<AdminApiMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = AdminRequestId.Get(context);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[AdminRequestId.Header] = requestId;
            context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });

        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
        {
            logger.LogError(ex, "[admin-api] {RequestId} {Method} {Path} failed", requestId, context.Request.Method, context.Request.Path.Value);
            context.Response.Clear();
            await AdminProblem.WriteAsync(context, StatusCodes.Status500InternalServerError, AdminProblemCodes.Internal);
            return;
        }

        var response = context.Response;
        if (!response.HasStarted && response.StatusCode >= 400 && response.ContentLength is null && string.IsNullOrEmpty(response.ContentType))
        {
            await AdminProblem.WriteAsync(context, response.StatusCode, AdminProblem.CodeFor(response.StatusCode));
        }
    }
}
