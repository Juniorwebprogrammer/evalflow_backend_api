using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Clarifications.GetMyClarifications;

public static class GetMyClarificationsEndpoint
{
    public static void MapGetMyClarifications(this IEndpointRouteBuilder app)
    {
        app.MapGet("/clarifications/mine", async (IMediator mediator) =>
                await mediator.Send(new GetMyClarificationsRecord()))
            .WithTags("Clarifications")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}
