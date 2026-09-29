using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace evalflow_backend_api.Features.AiAnalysis.GetCycleAiAnalyses;

public static class GetCycleAiAnalysesEndpoint
{
    public static void MapGetCycleAiAnalyses(this IEndpointRouteBuilder app)
    {
        app.MapGet("/evaluation-cycles/{cycleId:int}/ai-analysis", async (int cycleId, int? evaluatedUserId, IMediator mediator) =>
                await mediator.Send(new GetCycleAiAnalysesRecord(cycleId, evaluatedUserId)))
            .WithTags("AI Analysis")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}
