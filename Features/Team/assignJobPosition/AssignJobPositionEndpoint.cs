using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Team.AssignJobPosition;

public static class AssignJobPositionEndpoint
{
    public static void MapAssignJobPosition(this IEndpointRouteBuilder app)
    {
        app.MapPut("/team/{userId:int}/job-position", async (int userId, [FromBody] AssignJobPositionBody body, IMediator mediator) =>
            {
                return await mediator.Send(new AssignJobPositionRecord(userId, body.JobPositionId));
            })
            .WithTags("Team")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}