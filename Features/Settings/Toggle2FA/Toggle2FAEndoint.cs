using evalflow_backend_api.Infrastructure.Security;
using MediatR;

namespace evalflow_backend_api.Features.Settings.Toggle2FA;

public static class Toggle2FAEndpoint
{
    public static void MapToggle2FA(this IEndpointRouteBuilder app)
    {
        app.MapPut("/settings/2fa", async (Toggle2FARecord request, IMediator mediator) =>
            {
                return await mediator.Send(request);
            })
            .WithTags("Settings")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(); 
    }
}