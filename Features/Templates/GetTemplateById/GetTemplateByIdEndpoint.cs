using evalflow_backend_api.Infrastructure.Security;
using MediatR;

namespace evalflow_backend_api.Features.Templates.GetTemplateById;

public static class GetTemplateByIdEndpoint
{
    public static void MapGetTemplateById(this IEndpointRouteBuilder app)
    {
        app.MapGet("/templates/{id:int}", async (int id, IMediator mediator) =>
            {
                return await mediator.Send(new GetTemplateByIdRecord(id));
            })
            .WithTags("Templates")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}