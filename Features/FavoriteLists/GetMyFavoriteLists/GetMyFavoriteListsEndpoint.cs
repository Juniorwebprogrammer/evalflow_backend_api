using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.FavoriteLists.GetMyFavoriteLists;

public static class GetMyFavoriteListsEndpoint
{
    public static void MapGetMyFavoriteLists(this IEndpointRouteBuilder app)
    {
        app.MapGet("/favorite-lists", async (IMediator mediator) =>
                await mediator.Send(new GetMyFavoriteListsRecord()))
            .WithTags("Favorite Lists")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}