using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Team.GetSubordinates;

public static class GetSubordinatesEndpoint
{
    public static void MapGetSubordinates(this IEndpointRouteBuilder app)
    {
        app.MapGet("/team/{userId:int}/subordinates", async (int userId, IMediator mediator) =>
            {
                return await mediator.Send(new GetSubordinatesRecord(userId));
            })
            .WithTags("Team")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}