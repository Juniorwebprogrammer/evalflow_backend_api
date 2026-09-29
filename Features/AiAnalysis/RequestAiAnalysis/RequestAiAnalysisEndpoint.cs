using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.AiAnalysis.AiAnalysisDto;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace evalflow_backend_api.Features.AiAnalysis.RequestAiAnalysis;

public static class RequestAiAnalysisEndpoint
{
    public static void MapRequestAiAnalysis(this IEndpointRouteBuilder app)
    {
        app.MapPost("/evaluation-cycles/{cycleId:int}/ai-analysis", async (int cycleId, RequestAiAnalysisBody? body, IMediator mediator) =>
                await mediator.Send(new RequestAiAnalysisRecord(cycleId, body?.EvaluatedUserId, body?.TemplateId, body?.Force ?? false)))
            .WithTags("AI Analysis")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}
