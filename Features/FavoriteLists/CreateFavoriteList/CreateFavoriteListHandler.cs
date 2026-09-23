using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.FavoriteLists.CreateFavoriteList;

public class CreateFavoriteListHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<CreateFavoriteListRecord, IResult>
{
    public async Task<IResult> Handle(CreateFavoriteListRecord request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Results.Unauthorized();

        var newListId = await CreateAndSaveFavoriteListAsync(request, userId.Value, cancellationToken);

        return Results.Created($"/favorite-lists/{newListId}", new { Message = "Lista de favoritos creada con éxito.", Id = newListId });
    }

    private int? GetUserId()
    {
        var userIdStr = currentUser.GetUserId();
        return int.TryParse(userIdStr, out var userId) ? userId : null;
    }

    private async Task<int> CreateAndSaveFavoriteListAsync(CreateFavoriteListRecord request, int userId, CancellationToken cancellationToken)
    {
        var newList = new TemplateFavoriteList
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            UserId = userId
        };

        dbContext.TemplateFavoriteLists.Add(newList);
        await dbContext.SaveChangesAsync(cancellationToken);

        return newList.Id;
    }
}