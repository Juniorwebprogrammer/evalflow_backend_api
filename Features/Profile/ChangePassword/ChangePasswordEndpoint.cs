using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace evalflow_backend_api.Features.Profile.ChangePassword;

public static class ChangePasswordEndpoint
{
    public static void MapChangePassword(this IEndpointRouteBuilder app)
    {
        app.MapPut("/Profile/change-password", async ([FromBody] ChangePasswordRecord record, ISender sender) => await sender.Send(record))
            .WithTags("Profile")
            .WithName("ChangePassword")
            .WithSummary("Permite al usuario cambiar su propia contraseña")
            .WithDescription("Requiere API Key y JWT válido. No requiere roles específicos.")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}