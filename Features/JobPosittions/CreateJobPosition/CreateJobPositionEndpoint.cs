using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.JobPosittions.CreateJobPosition;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.JobPositions.CreateJobPosition;

public static class CreateJobPositionEndpoint
{
    public static void MapCreateJobPosition(this IEndpointRouteBuilder app)
    {
        app.MapPost("/job-positions", async ([FromBody] CreateJobPositionRecord request, IMediator mediator) =>
            {
                return await mediator.Send(request);
            })
            .WithTags("Job Positions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}