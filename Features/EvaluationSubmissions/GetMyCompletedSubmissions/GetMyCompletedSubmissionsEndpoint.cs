using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GetMyCompletedSubmissions;

public static class GetMyCompletedSubmissionsEndpoint
{
    public static void MapGetMyCompletedSubmissions(this IEndpointRouteBuilder app)
    {
        app.MapGet("/evaluation-submissions/completed", async (IMediator mediator) =>
                await mediator.Send(new GetMyCompletedSubmissionsRecord()))
            .WithTags("Evaluation Submissions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}