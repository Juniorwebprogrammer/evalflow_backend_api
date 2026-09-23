using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.Templates.UpdateTemplates;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Templates.UpdateTemplate;

public static class UpdateTemplateEndpoint
{
    public static void MapUpdateTemplate(this IEndpointRouteBuilder app)
    {
        app.MapPut("/templates/{id:int}", async (int id, [FromBody] UpdateTemplateBody body, IMediator mediator) =>
            {
                return await mediator.Send(new UpdateTemplateRecord(id, body.Titulo, body.Descripcion, body.FechaInicio, body.FechaFin, body.AssignedUserIds));
            })
            .WithTags("Templates")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}