using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.FavoriteLists.DeleteFavoriteList;

public static class DeleteFavoriteListEndpoint
{
    public static void MapDeleteFavoriteList(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/favorite-lists/{id:int}", async (int id, IMediator mediator) =>
                await mediator.Send(new DeleteFavoriteListRecord(id)))
            .WithTags("Favorite Lists")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}