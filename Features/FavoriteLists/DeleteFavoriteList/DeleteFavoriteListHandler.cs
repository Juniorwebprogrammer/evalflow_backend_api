using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.FavoriteLists.DeleteFavoriteList;

public class DeleteFavoriteListHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<DeleteFavoriteListRecord, IResult>
{
    public async Task<IResult> Handle(DeleteFavoriteListRecord request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Results.Unauthorized();

        var list = await GetFavoriteListAsync(request.Id, userId.Value, cancellationToken);
        if (list is null) return Results.NotFound(new { Message = "Lista no encontrada." });

        await DeleteAndSaveListAsync(list, cancellationToken);

        return Results.Ok(new { Message = "Lista eliminada correctamente." });
    }

    private int? GetUserId()
    {
        var userIdStr = currentUser.GetUserId();
        return int.TryParse(userIdStr, out var userId) ? userId : null;
    }

    private async Task<TemplateFavoriteList?> GetFavoriteListAsync(int listId, int userId, CancellationToken cancellationToken)
    {
        return await dbContext.TemplateFavoriteLists
            .FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId, cancellationToken);
    }

    private async Task DeleteAndSaveListAsync(TemplateFavoriteList list, CancellationToken cancellationToken)
    {
        dbContext.TemplateFavoriteLists.Remove(list);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}