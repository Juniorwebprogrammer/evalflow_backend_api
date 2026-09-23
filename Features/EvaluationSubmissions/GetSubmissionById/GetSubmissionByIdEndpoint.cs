using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GetSubmissionById;

public static class GetSubmissionByIdEndpoint
{
    public static void MapGetSubmissionById(this IEndpointRouteBuilder app)
    {
        app.MapGet("/evaluation-submissions/{submissionId:int}", async (int submissionId, IMediator mediator) =>
                await mediator.Send(new GetSubmissionByIdRecord(submissionId)))
            .WithTags("Evaluation Submissions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}