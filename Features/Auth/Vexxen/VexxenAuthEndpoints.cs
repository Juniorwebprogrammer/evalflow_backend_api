using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace evalflow_backend_api.Features.Auth.Vexxen;

public static class VexxenAuthEndpoints
{
    public static void MapVexxenAuth(this IEndpointRouteBuilder app)
    {
        app.MapPost("/Auth/vexxen", async ([FromBody] VexxenSignInRecord record, ISender sender) => await sender.Send(record))
            .WithTags("Auth")
            .WithName("SignInWithVexxen")
            .WithSummary("Inicia sesión con Vexxen")
            .WithDescription("Valida el access token de Vexxen y devuelve el JWT de EvalFlow, o needs_onboarding si aún no tiene empresa.")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .AllowAnonymous();

        app.MapPost("/Auth/refresh", async ([FromBody] RefreshSessionRecord record, ISender sender) => await sender.Send(record))
            .WithTags("Auth")
            .WithName("RefreshSession")
            .WithSummary("Renueva la sesión con el refresh token de Vexxen")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .AllowAnonymous();

        app.MapPost("/Auth/logout", async ([FromBody] LogoutRecord record, ISender sender) => await sender.Send(record))
            .WithTags("Auth")
            .WithName("Logout")
            .WithSummary("Cierra la sesión (revoca el refresh token de Vexxen)")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .AllowAnonymous();
    }
}
