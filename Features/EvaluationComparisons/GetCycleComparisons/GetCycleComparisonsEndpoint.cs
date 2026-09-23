using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationComparisons.GetCycleComparisons;

public static class GetCycleComparisonsEndpoint
{
    public static void MapGetCycleComparisons(this IEndpointRouteBuilder app)
    {
        app.MapGet("/evaluation-cycles/{cycleId:int}/comparisons", async (int cycleId, int? evaluatedUserId, IMediator mediator) =>
                await mediator.Send(new GetCycleComparisonsRecord(cycleId, evaluatedUserId)))
            .WithTags("Evaluation Comparisons")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}
