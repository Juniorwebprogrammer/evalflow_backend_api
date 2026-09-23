using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.FavoriteLists.ToggleTemplateInList;

public static class ToggleTemplateInListEndpoint
{
    public static void MapToggleTemplateInList(this IEndpointRouteBuilder app)
    {
        app.MapPut("/favorite-lists/{listId:int}/templates/{templateId:int}/toggle", async (int listId, int templateId, IMediator mediator) =>
                await mediator.Send(new ToggleTemplateInListRecord(listId, templateId)))
            .WithTags("Favorite Lists")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}