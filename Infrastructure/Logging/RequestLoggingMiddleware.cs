using System.Diagnostics;

namespace evalflow_backend_api.Infrastructure.Logging;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
            LogRequest(context, context.Response.StatusCode, startedAt);
        }
        catch
        {
            LogRequest(context, StatusCodes.Status500InternalServerError, startedAt);
            throw;
        }
    }

    private void LogRequest(HttpContext context, int statusCode, long startedAt)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

        logger.Log(
            GetLogLevel(statusCode),
            "{Method} {Path} {StatusCode} {ElapsedMs:0.0} ms",
            context.Request.Method,
            context.Request.Path.Value,
            statusCode,
            elapsedMs);
    }

    private static LogLevel GetLogLevel(int statusCode)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError) return LogLevel.Error;
        if (statusCode >= StatusCodes.Status400BadRequest) return LogLevel.Warning;

        return LogLevel.Information;
    }
}
