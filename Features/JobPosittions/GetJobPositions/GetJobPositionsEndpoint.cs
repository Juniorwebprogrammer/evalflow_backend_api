using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.JobPositions.GetJobPositions;

public static class GetJobPositionsEndpoint
{
    public static void MapGetJobPositions(this IEndpointRouteBuilder app)
    {
        app.MapGet("/job-positions", async (IMediator mediator) =>
            {
                return await mediator.Send(new GetJobPositionsRecord());
            })
            .WithTags("Job Positions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(); 
    }
}