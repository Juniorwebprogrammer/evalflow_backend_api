using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationResults.DownloadEvaluationResultPdf;

public static class DownloadEvaluationResultPdfEndpoint
{
    public static void MapDownloadEvaluationResultPdf(this IEndpointRouteBuilder app)
    {
        app.MapGet("/evaluation-results/{id:int}/pdf", async (int id, IMediator mediator) =>
                await mediator.Send(new DownloadEvaluationResultPdfRecord(id)))
            .WithTags("Evaluation Results")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}
