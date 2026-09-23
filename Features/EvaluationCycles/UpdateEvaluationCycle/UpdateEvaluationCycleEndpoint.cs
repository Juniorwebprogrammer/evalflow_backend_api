using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationCycles.UpdateEvaluationCycle;

public static class UpdateEvaluationCycleEndpoint
{
    public static void MapUpdateEvaluationCycle(this IEndpointRouteBuilder app)
    {
        app.MapPut("/evaluation-cycles/{id:int}", async (int id, [FromBody] UpdateEvaluationCycleBody body, IMediator mediator) =>
            await mediator.Send(new UpdateEvaluationCycleRecord(id, body.Nombre, body.Descripcion, body.Activo, body.FechaInicio, body.FechaFin, body.TipoEvaluacion)))
            .WithTags("Evaluation Cycles")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}