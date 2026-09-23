using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationSubmissions.SaveSubmissionAnswers;

public static class SaveSubmissionAnswersEndpoint
{
    public static void MapSaveSubmissionAnswers(this IEndpointRouteBuilder app)
    {
        app.MapPut("/evaluation-submissions/{submissionId:int}/answers", async (int submissionId, [FromBody] SaveSubmissionAnswersBody body, IMediator mediator) =>
            await mediator.Send(new SaveSubmissionAnswersRecord(submissionId, body.Answers)))
            .WithTags("Evaluation Submissions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}