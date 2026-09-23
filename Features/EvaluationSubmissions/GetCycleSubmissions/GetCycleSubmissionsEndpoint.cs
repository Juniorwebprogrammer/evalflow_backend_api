using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GetCycleSubmissions;

public static class GetCycleSubmissionsEndpoint
{
    public static void MapGetCycleSubmissions(this IEndpointRouteBuilder app)
    {
        app.MapGet("/evaluation-cycles/{cycleId:int}/submissions", async (int cycleId, IMediator mediator) =>
                await mediator.Send(new GetCycleSubmissionsRecord(cycleId)))
            .WithTags("Evaluation Submissions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}
