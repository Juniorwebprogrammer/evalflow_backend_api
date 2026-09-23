using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Auth.ForgotPassword;

public static class ForgotPasswordEndpoint
{
    public static void MapForgotPassword(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/forgot-password", async ([FromBody] ForgotPasswordBody body, IMediator mediator) =>
            {
                return await mediator.Send(new ForgotPasswordRecord(body.Email));
            })
            .WithTags("Auth")
            .AddEndpointFilter<ApiKeyEndpointFilter>(); // 👈 Protección estricta con API Key
    }
}