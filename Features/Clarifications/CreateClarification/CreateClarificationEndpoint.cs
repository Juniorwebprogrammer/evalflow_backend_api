using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Clarifications.CreateClarification;

public static class CreateClarificationEndpoint
{
    public static void MapCreateClarification(this IEndpointRouteBuilder app)
    {
        app.MapPost("/evaluation-cycles/{cycleId:int}/clarifications", async (int cycleId, [FromBody] CreateClarificationBody body, IMediator mediator) =>
                await mediator.Send(new CreateClarificationRecord(cycleId, body.EvaluatedUserId, body.TemplateId, body.QuestionId, body.Mensaje)))
            .WithTags("Clarifications")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}
