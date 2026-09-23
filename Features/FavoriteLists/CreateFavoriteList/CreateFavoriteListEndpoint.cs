using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.FavoriteLists.CreateFavoriteList;

public static class CreateFavoriteListEndpoint
{
    public static void MapCreateFavoriteList(this IEndpointRouteBuilder app)
    {
        app.MapPost("/favorite-lists", async ([FromBody] CreateFavoriteListBody body, IMediator mediator) =>
            await mediator.Send(new CreateFavoriteListRecord(body.Nombre, body.Descripcion)))
            .WithTags("Favorite Lists")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}