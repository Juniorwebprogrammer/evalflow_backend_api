using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace evalflow_backend_api.Features.Team.AcceptInvite;

public static class AcceptInviteEndpoint
{
    public static void MapAcceptInvite(this IEndpointRouteBuilder app)
    {
        app.MapPost("/team/accept-invite", async ([FromBody] AcceptInviteRecord record, IMediator mediator) => await mediator.Send(record))
            .WithTags("Team")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .AllowAnonymous();
    }
}