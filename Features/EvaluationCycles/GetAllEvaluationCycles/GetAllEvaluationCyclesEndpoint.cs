using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationCycles.GetAllEvaluationCycles;

public static class GetAllEvaluationCyclesEndpoint
{
    public static void MapGetAllEvaluationCycles(this IEndpointRouteBuilder app)
    {
        app.MapGet("/evaluation-cycles", async (IMediator mediator) =>
                await mediator.Send(new GetAllEvaluationCyclesRecord()))
            .WithTags("Evaluation Cycles")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}