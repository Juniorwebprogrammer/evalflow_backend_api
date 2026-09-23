using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.FavoriteLists.UpdateFavoriteList;

public static class UpdateFavoriteListEndpoint
{
    public static void MapUpdateFavoriteList(this IEndpointRouteBuilder app)
    {
        app.MapPut("/favorite-lists/{id:int}", async (int id, [FromBody] UpdateFavoriteListBody body, IMediator mediator) =>
            await mediator.Send(new UpdateFavoriteListRecord(id, body.Nombre, body.Descripcion)))
            .WithTags("Favorite Lists")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}