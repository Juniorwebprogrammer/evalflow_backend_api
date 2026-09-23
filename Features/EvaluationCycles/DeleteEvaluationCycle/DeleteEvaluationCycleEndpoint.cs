using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationCycles.DeleteEvaluationCycle;

public static class DeleteEvaluationCycleEndpoint
{
    public static void MapDeleteEvaluationCycle(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/evaluation-cycles/{id:int}", async (int id, IMediator mediator) =>
                await mediator.Send(new DeleteEvaluationCycleRecord(id)))
            .WithTags("Evaluation Cycles")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}