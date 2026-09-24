using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationResults.GetMyEvaluationResults;

public static class GetMyEvaluationResultsEndpoint
{
    public static void MapGetMyEvaluationResults(this IEndpointRouteBuilder app)
    {
        app.MapGet("/evaluation-results/mine", async (IMediator mediator) =>
                await mediator.Send(new GetMyEvaluationResultsRecord()))
            .WithTags("Evaluation Results")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}
