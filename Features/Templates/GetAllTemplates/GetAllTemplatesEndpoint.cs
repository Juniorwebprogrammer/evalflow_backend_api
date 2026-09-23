using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Templates.GetAllTemplates;

public static class GetAllTemplatesEndpoint
{
    public static void MapGetAllTemplates(this IEndpointRouteBuilder app)
    {
        app.MapGet("/templates", async (IMediator mediator) =>
            {
                return await mediator.Send(new GetAllTemplatesRecord());
            })
            .WithTags("Templates")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}