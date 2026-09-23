using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.Templates.DeleteTemplates;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Templates.DeleteTemplate;

public static class DeleteTemplateEndpoint
{
    public static void MapDeleteTemplate(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/templates/{id:int}", async (int id, IMediator mediator) =>
            {
                return await mediator.Send(new DeleteTemplateRecord(id));
            })
            .WithTags("Templates")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}