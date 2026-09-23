using evalflow_backend_api.Infrastructure.Security;
using MediatR;

namespace evalflow_backend_api.Features.Auth.Verify2FA;

public static class Verify2FAEndpoint
{
    public static void MapVerify2FA(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/verify-2fa", async (Verify2FARecord request, IMediator mediator) =>
            {
                return await mediator.Send(request);
            })
            .WithTags("Auth")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .AllowAnonymous(); 
    }
}