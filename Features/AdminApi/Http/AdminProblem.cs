using System.Text.Json;
using System.Text.Json.Serialization;
using evalflow_backend_api.Features.AdminApi.Contract;

namespace evalflow_backend_api.Features.AdminApi.Http;

public sealed record AdminProblemBody(
    string Type,
    string Title,
    int Status,
    string Code,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detail,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RequestId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<AdminFieldError>? Errors);

/// <summary>Admin API errors as problem+json (RFC 9457), as the contract asks (§6.7): { type, title, status, code, detail?, requestId, errors? }.</summary>
public static class AdminProblem
{
    public const string ContentType = "application/problem+json";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string> Titles = new()
    {
        [AdminProblemCodes.Unauthorized] = "Unauthorized",
        [AdminProblemCodes.Forbidden] = "Forbidden",
        [AdminProblemCodes.UnsupportedGrant] = "Unsupported grant",
        [AdminProblemCodes.NotFound] = "Not found",
        [AdminProblemCodes.InvalidQuery] = "Invalid query",
        [AdminProblemCodes.InvalidInput] = "Invalid input",
        [AdminProblemCodes.Conflict] = "Conflict",
        [AdminProblemCodes.MethodNotAllowed] = "Method not allowed",
        [AdminProblemCodes.Internal] = "Internal error",
    };

    public static AdminProblemBody Body(HttpContext context, int status, string code, string? detail = null,
        IReadOnlyList<AdminFieldError>? errors = null) =>
        new("about:blank", Titles.GetValueOrDefault(code, "Error"), status, code, detail, AdminRequestId.Get(context),
            errors is { Count: > 0 } ? errors : null);

    public static IResult Result(HttpContext context, int status, string code, string? detail = null, IReadOnlyList<AdminFieldError>? errors = null) =>
        Results.Json(Body(context, status, code, detail, errors), Json, ContentType, status);

    public static IResult From(HttpContext context, AdminApiException error) => Result(context, error.Status, error.Code, error.Message, error.Errors);

    public static Task WriteAsync(HttpContext context, int status, string code, string? detail = null)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(Body(context, status, code, detail), Json, ContentType, context.RequestAborted);
    }

    /// <summary>The code for an error the framework answered without a body (routing, authentication…).</summary>
    public static string CodeFor(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => AdminProblemCodes.Unauthorized,
        StatusCodes.Status403Forbidden => AdminProblemCodes.Forbidden,
        StatusCodes.Status404NotFound => AdminProblemCodes.NotFound,
        StatusCodes.Status405MethodNotAllowed => AdminProblemCodes.MethodNotAllowed,
        StatusCodes.Status409Conflict => AdminProblemCodes.Conflict,
        >= 400 and < 500 => AdminProblemCodes.InvalidQuery,
        _ => AdminProblemCodes.Internal,
    };
}

/// <summary>X-Request-Id: the caller's, if it sends one, or a new one. It goes in every response, error and log line.</summary>
public static class AdminRequestId
{
    public const string Header = "X-Request-Id";
    private const string ItemKey = "AdminApi.RequestId";
    private const int MaxLength = 100;

    public static string Get(HttpContext context)
    {
        if (context.Items[ItemKey] is string existing) return existing;

        var given = context.Request.Headers[Header].ToString().Trim();
        var requestId = given.Length > 0 && given.All(c => c is > ' ' and < (char)127)
            ? given[..Math.Min(given.Length, MaxLength)]
            : Guid.NewGuid().ToString();
        context.Items[ItemKey] = requestId;
        return requestId;
    }
}
