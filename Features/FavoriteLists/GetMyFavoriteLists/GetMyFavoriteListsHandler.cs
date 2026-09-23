using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.FavoriteLists.GetMyFavoriteLists;

public class GetMyFavoriteListsHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetMyFavoriteListsRecord, IResult>
{
    public async Task<IResult> Handle(GetMyFavoriteListsRecord request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Results.Unauthorized();

        var lists = await FetchFavoriteListsAsync(userId.Value, cancellationToken);

        return Results.Ok(lists);
    }

    private int? GetUserId()
    {
        var userIdStr = currentUser.GetUserId();
        return int.TryParse(userIdStr, out var userId) ? userId : null;
    }

    private async Task<List<FavoriteListDto.FavoriteListDto>> FetchFavoriteListsAsync(int userId, CancellationToken cancellationToken)
    {
        return await dbContext.TemplateFavoriteLists
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.Nombre)
            .Select(l => new FavoriteListDto.FavoriteListDto(
                l.Id,
                l.Nombre,
                l.Descripcion,
                l.Templates.Count(),
                l.Templates.Select(t => t.Id).ToList()
            ))
            .ToListAsync(cancellationToken);
    }
}