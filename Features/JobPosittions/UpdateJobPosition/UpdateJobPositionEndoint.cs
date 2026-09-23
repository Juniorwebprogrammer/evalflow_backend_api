using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.JobPosittions.UpdateJobPosition;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.JobPositions.ManageJobPositions;

public static class UpdateJobPositionEndpoint
{
    public static void MapUpdateJobPosition(this IEndpointRouteBuilder app)
    {
        app.MapPut("/job-positions/{id:int}", async (int id, [FromBody] UpdateJobPositionBody body, IMediator mediator) =>
            {
                return await mediator.Send(new UpdateJobPositionRecord(id, body.Nombre, body.Descripcion));
            })
            .WithTags("Job Positions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}