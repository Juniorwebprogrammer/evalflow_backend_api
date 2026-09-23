    using evalflow_backend_api.Infrastructure.Security;
    using MediatR;

    namespace evalflow_backend_api.Features.Auth.VerifyEmail;

    public static class VerifyEmailEndpoint
    {
        public static void MapVerifyEmail(this IEndpointRouteBuilder app)
        {
            app.MapPost("/auth/verify-email", async (VerifyEmailRecord request, IMediator mediator) =>
                {
                    return await mediator.Send(request);
                })
                .WithTags("Auth")
                .AddEndpointFilter<ApiKeyEndpointFilter>()
                .AllowAnonymous();
        }
    }