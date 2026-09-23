using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationCycles.CreateEvaluationCycle;

public static class CreateEvaluationCycleEndpoint
{
    public static void MapCreateEvaluationCycle(this IEndpointRouteBuilder app)
    {
        app.MapPost("/evaluation-cycles", async ([FromBody] CreateEvaluationCycleBody body, IMediator mediator) =>
            {
                return await mediator.Send(new CreateEvaluationCycleRecord(body.Nombre, body.Descripcion, body.FechaInicio, body.FechaFin, body.TipoEvaluacion));
            })
            .WithTags("Evaluation Cycles")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" }); // Solo admins crean ciclos
    }
}