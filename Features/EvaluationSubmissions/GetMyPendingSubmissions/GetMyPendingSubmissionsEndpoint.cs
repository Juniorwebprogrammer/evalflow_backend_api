using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GetMyPendingSubmissions;

public static class GetMyPendingSubmissionsEndpoint
{
    public static void MapGetMyPendingSubmissions(this IEndpointRouteBuilder app)
    {
        app.MapGet("/evaluation-submissions/pending", async (IMediator mediator) =>
                await mediator.Send(new GetMyPendingSubmissionsRecord()))
            .WithTags("Evaluation Submissions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}