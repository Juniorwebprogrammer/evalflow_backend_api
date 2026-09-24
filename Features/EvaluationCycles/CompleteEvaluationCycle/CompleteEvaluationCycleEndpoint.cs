using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationCycles.CompleteEvaluationCycle;

public static class CompleteEvaluationCycleEndpoint
{
    public static void MapCompleteEvaluationCycle(this IEndpointRouteBuilder app)
    {
        app.MapPost("/evaluation-cycles/{cycleId:int}/complete", async (int cycleId, IMediator mediator) =>
                await mediator.Send(new CompleteEvaluationCycleRecord(cycleId)))
            .WithTags("Evaluation Cycles")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}
