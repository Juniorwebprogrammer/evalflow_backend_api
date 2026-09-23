using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GenerateSubmissions;

public static class GenerateSubmissionsEndpoint
{
    public static void MapGenerateSubmissions(this IEndpointRouteBuilder app)
    {
        app.MapPost("/evaluation-cycles/{cycleId:int}/generate-submissions", async (int cycleId, IMediator mediator) =>
                await mediator.Send(new GenerateSubmissionsRecord(cycleId)))
            .WithTags("Evaluation Submissions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}