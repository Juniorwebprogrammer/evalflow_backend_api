using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Clarifications.GetCycleClarifications;

public static class GetCycleClarificationsEndpoint
{
    public static void MapGetCycleClarifications(this IEndpointRouteBuilder app)
    {
        app.MapGet("/evaluation-cycles/{cycleId:int}/clarifications", async (int cycleId, int? evaluatedUserId, IMediator mediator) =>
                await mediator.Send(new GetCycleClarificationsRecord(cycleId, evaluatedUserId)))
            .WithTags("Clarifications")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}
