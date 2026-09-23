using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Auth.ResetPassword;

public static class ResetPasswordEndpoint
{
    public static void MapResetPassword(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/reset-password", async ([FromBody] ResetPasswordBody body, IMediator mediator) =>
            {
                return await mediator.Send(new ResetPasswordRecord(body.Token, body.NewPassword));
            })
            .WithTags("Auth")
            .AddEndpointFilter<ApiKeyEndpointFilter>();
    }
}