using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Team.AssignSuperior;

public static class AssignSuperiorEndpoint
{
    public static void MapAssignSuperior(this IEndpointRouteBuilder app)
    {
        app.MapPut("/team/{userId:int}/superior", async (int userId, [FromBody] AssignSuperiorBody body, IMediator mediator) =>
            {
                return await mediator.Send(new AssignSuperiorRecord(userId, body.SuperiorId));
            })
            .WithTags("Team")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}