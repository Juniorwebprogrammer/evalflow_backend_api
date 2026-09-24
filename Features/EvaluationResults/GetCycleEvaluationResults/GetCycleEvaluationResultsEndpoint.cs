using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationResults.GetCycleEvaluationResults;

public static class GetCycleEvaluationResultsEndpoint
{
    public static void MapGetCycleEvaluationResults(this IEndpointRouteBuilder app)
    {
        app.MapGet("/evaluation-cycles/{cycleId:int}/results", async (int cycleId, IMediator mediator) =>
                await mediator.Send(new GetCycleEvaluationResultsRecord(cycleId)))
            .WithTags("Evaluation Results")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}
