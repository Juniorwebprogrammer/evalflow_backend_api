using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationSubmissions.DeleteSubmission;

public static class DeleteSubmissionEndpoint
{
    public static void MapDeleteSubmission(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/evaluation-submissions/{submissionId:int}", async (int submissionId, IMediator mediator) =>
                await mediator.Send(new DeleteSubmissionRecord(submissionId)))
            .WithTags("Evaluation Submissions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}