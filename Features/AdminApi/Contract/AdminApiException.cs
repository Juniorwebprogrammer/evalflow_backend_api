namespace evalflow_backend_api.Features.AdminApi.Contract;

public sealed record AdminFieldError(string Field, string Message);

/// <summary>
/// An Admin API error with its contract code (§6.7, §12.3). The endpoint filter turns it into problem+json; anything
/// else that escapes is a 500 internal without details.
/// </summary>
public sealed class AdminApiException(int status, string code, string detail, IReadOnlyList<AdminFieldError>? errors = null)
    : Exception(detail)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public IReadOnlyList<AdminFieldError> Errors { get; } = errors ?? [];

    public static AdminApiException InvalidQuery(string detail, IReadOnlyList<AdminFieldError> errors) =>
        new(StatusCodes.Status400BadRequest, AdminProblemCodes.InvalidQuery, detail, errors);

    public static AdminApiException InvalidInput(string detail, IReadOnlyList<AdminFieldError> errors) =>
        new(StatusCodes.Status400BadRequest, AdminProblemCodes.InvalidInput, detail, errors);

    public static AdminApiException NotFound(string detail) => new(StatusCodes.Status404NotFound, AdminProblemCodes.NotFound, detail);

    public static AdminApiException Forbidden(string detail) => new(StatusCodes.Status403Forbidden, AdminProblemCodes.Forbidden, detail);

    public static AdminApiException Conflict(string detail) => new(StatusCodes.Status409Conflict, AdminProblemCodes.Conflict, detail);
}

public static class AdminProblemCodes
{
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string UnsupportedGrant = "unsupported_grant";
    public const string NotFound = "not_found";
    public const string InvalidQuery = "invalid_query";
    public const string InvalidInput = "invalid_input";
    public const string Conflict = "conflict";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string Internal = "internal";
}
