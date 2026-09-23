using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationCycles.ToggleTemplateInCycle;

public static class ToggleTemplateInCycleEndpoint
{
    public static void MapToggleTemplateInCycle(this IEndpointRouteBuilder app)
    {
        app.MapPut("/evaluation-cycles/{cycleId:int}/templates/{templateId:int}/toggle", async (int cycleId, int templateId, IMediator mediator) =>
                await mediator.Send(new ToggleTemplateInCycleRecord(cycleId, templateId)))
            .WithTags("Evaluation Cycles")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}