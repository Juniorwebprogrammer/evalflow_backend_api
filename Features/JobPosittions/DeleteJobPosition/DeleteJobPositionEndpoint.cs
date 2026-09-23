using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.JobPosittions.DeleteJobPosition;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.JobPositions.ManageJobPositions;

public static class DeleteJobPositionEndpoint
{
    public static void MapDeleteJobPosition(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/job-positions/{id:int}", async (int id, IMediator mediator) =>
            {
                return await mediator.Send(new DeleteJobPositionRecord(id));
            })
            .WithTags("Job Positions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}