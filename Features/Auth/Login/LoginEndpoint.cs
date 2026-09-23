using MediatR;
using Microsoft.AspNetCore.Mvc;
using evalflow_backend_api.Infrastructure.Security;

namespace evalflow_backend_api.Features.Auth.Login;

public static class LoginEndpoint
{
    public static void MapLogin(this IEndpointRouteBuilder app)
    {
        app.MapPost("/Auth/login", async ([FromBody] LoginRecord record, ISender sender) =>
            {
                return await sender.Send(record);
            })
            .WithTags("Auth")
            .WithName("Login")
            .WithSummary("Inicia sesión de usuario")
            .WithDescription("Valida credenciales y entorno (LocationId) para devolver un JWT y Refresh Token.")
            .AddEndpointFilter<ApiKeyEndpointFilter>();
    }
}