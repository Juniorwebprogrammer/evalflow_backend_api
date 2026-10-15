namespace evalflow_backend_api.Features.Auth.Vexxen;

/// <summary>
/// The old email/password flows. Vexxen owns accounts now (password, 2FA, email verification, name), so these
/// answer 410 Gone with a stable code instead of a confusing 404.
/// </summary>
public static class RetiredAuthEndpoints
{
    private static readonly (string Method, string Route)[] Routes =
    [
        ("POST", "/Auth/login"),
        ("POST", "/auth/verify-email"),
        ("POST", "/auth/resend-verification"),
        ("POST", "/auth/verify-2fa"),
        ("POST", "/auth/resend-2fa"),
        ("POST", "/auth/forgot-password"),
        ("POST", "/auth/reset-password"),
        ("PUT", "/settings/2fa"),
        ("PUT", "/Profile/change-password"),
        ("PUT", "/Profile/update"),
        ("POST", "/team/accept-invite"),
    ];

    public static void MapRetiredAuth(this IEndpointRouteBuilder app)
    {
        foreach (var (method, route) in Routes)
        {
            app.MapMethods(route, [method], () => Results.Json(
                    new { Code = "USE_VEXXEN", Message = "EvalFlow usa ahora tu cuenta de Vexxen: inicia sesión con Vexxen y gestiona allí tu contraseña, tu nombre y la verificación en dos pasos." },
                    statusCode: StatusCodes.Status410Gone))
                .WithTags("Auth")
                .ExcludeFromDescription()
                .AllowAnonymous();
        }
    }
}
