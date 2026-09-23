using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Team.ToggleUserStatus;

public static class ToggleUserStatusEndpoint
{
    public static void MapToggleUserStatus(this IEndpointRouteBuilder app)
    {
        app.MapPut("/team/{userId:int}/status", async (int userId, [FromBody] ToggleUserStatusBody body, IMediator mediator) =>
            {
                return await mediator.Send(new ToggleUserStatusRecord(userId, body.Activo));
            })
            .WithTags("Team")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}