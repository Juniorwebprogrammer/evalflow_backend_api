using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Clarifications.RespondClarification;

public static class RespondClarificationEndpoint
{
    public static void MapRespondClarification(this IEndpointRouteBuilder app)
    {
        app.MapPut("/clarifications/{id:int}/response", async (int id, [FromBody] RespondClarificationBody body, IMediator mediator) =>
                await mediator.Send(new RespondClarificationRecord(id, body.Respuesta)))
            .WithTags("Clarifications")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}
