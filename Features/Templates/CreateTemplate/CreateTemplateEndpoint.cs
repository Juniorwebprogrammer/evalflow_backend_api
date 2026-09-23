using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace evalflow_backend_api.Features.Templates.CreateTemplate;

public static class CreateTemplateEndpoint
{
    public static void MapCreateTemplate(this IEndpointRouteBuilder app)
    {
        app.MapPost("/templates", async ([FromBody] CreateTemplateBody body, IMediator mediator) =>
            {
                return await mediator.Send(new CreateTemplateRecord(body.Titulo, body.Descripcion, body.FechaInicio, body.FechaFin, body.AssignedUserIds));
            })
            .WithTags("Templates")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}