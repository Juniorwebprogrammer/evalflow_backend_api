namespace evalflow_backend_api.Features.Auth.Vexxen;

/// <summary>Error responses of the Vexxen sign-in flow: { code, message }, with a stable code for the frontend.</summary>
public static class VexxenErrors
{
    public static IResult Error(int status, string code, string message) =>
        Results.Json(new { Code = code, Message = message }, statusCode: status);

    /// <summary>Vexxen is down or misconfigured: signing in isn't possible right now (open sessions keep working).</summary>
    public static IResult Unavailable() =>
        Error(StatusCodes.Status503ServiceUnavailable, "VEXXEN_UNAVAILABLE", "No podemos conectar con Vexxen. Inténtalo de nuevo en unos minutos.");

    /// <summary>
    /// The Vexxen account is suspended: no EvalFlow session (403 on sign-in, 401 on refresh, where the session ends).
    /// </summary>
    public static IResult Suspended(int status) =>
        Error(status, "ACCOUNT_SUSPENDED", "Tu cuenta de Vexxen está suspendida. Si crees que es un error, contacta con soporte.");

    public static IResult InvalidSession() =>
        Error(StatusCodes.Status401Unauthorized, "INVALID_VEXXEN_SESSION", "Tu sesión de Vexxen no es válida o ha caducado. Vuelve a iniciar sesión.");
}
