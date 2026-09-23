using MediatR;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using evalflow_backend_api.Infrastructure.Security;

namespace evalflow_backend_api.Features.Auth.GetMyFeatures;

public static class GetMyFeaturesEndpoint
{
    public static void MapGetMyFeatures(this IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/my-features", async (HttpContext context, IMediator mediator) =>
            {
                var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;

                if (string.IsNullOrEmpty(roleClaim))
                {
                    return Results.Unauthorized();
                }

                var query = new GetMyFeaturesRecord(roleClaim);
                return await mediator.Send(query);
            })
            .WithTags("Auth")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}