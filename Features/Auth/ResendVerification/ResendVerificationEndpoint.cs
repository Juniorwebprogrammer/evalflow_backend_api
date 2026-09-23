using evalflow_backend_api.Infrastructure.Security;
using MediatR;

namespace evalflow_backend_api.Features.Auth.ResendVerification;

public static class ResendVerificationEndpoint
{
    public static void MapResendVerification(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/resend-verification", async (ResendVerificationRecord request, IMediator mediator) =>
            {
                return await mediator.Send(request);
            })
            .WithTags("Auth")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .AllowAnonymous();
    }
}