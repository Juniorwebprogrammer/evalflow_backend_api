using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.FavoriteLists.UpdateFavoriteList;

public class UpdateFavoriteListHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<UpdateFavoriteListRecord, IResult>
{
    public async Task<IResult> Handle(UpdateFavoriteListRecord request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Results.Unauthorized();

        var list = await GetFavoriteListAsync(request.Id, userId.Value, cancellationToken);
        if (list is null) return Results.NotFound(new { Message = "Lista no encontrada." });

        await UpdateAndSaveListAsync(list, request, cancellationToken);

        return Results.Ok(new { Message = "Lista actualizada correctamente." });
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

    private async Task UpdateAndSaveListAsync(TemplateFavoriteList list, UpdateFavoriteListRecord request, CancellationToken cancellationToken)
    {
        list.Nombre = request.Nombre;
        list.Descripcion = request.Descripcion;

        dbContext.TemplateFavoriteLists.Update(list);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}