using evalflow_backend_api.Infrastructure.Security;
using MediatR;

namespace evalflow_backend_api.Features.Auth.Resend2FA;

public static class Resend2FAEndpoint
{
    public static void MapResend2FA(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/resend-2fa", async (Resend2FARecord request, IMediator mediator) =>
            {
                return await mediator.Send(request);
            })
            .WithTags("Auth")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .AllowAnonymous();
    }
}