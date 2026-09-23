using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Dashboard.GetDashboardStats;

public static class GetDashboardStatsEndpoint
{
    public static void MapGetDashboardStats(this IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/stats", async (IMediator mediator) =>
                await mediator.Send(new GetDashboardStatsRecord()))
            .WithTags("Dashboard")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}
