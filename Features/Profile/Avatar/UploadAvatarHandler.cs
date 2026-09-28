using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Profile.Avatar;

public class UploadAvatarHandler(AppDbContext dbContext, ICurrentUserService currentUserService)
    : IRequestHandler<UploadAvatarRecord, IResult>
{
    public async Task<IResult> Handle(UploadAvatarRecord request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(currentUserService.GetUserId(), out var userId)) return Results.Unauthorized();

        var image = AvatarImage.TryDecode(request.Data);
        if (image is null)
        {
            return Results.BadRequest(new
            {
                Message = $"La imagen no es válida. Usa un JPG, PNG o WebP de hasta {AvatarImage.MaxBytes / 1024} KB."
            });
        }

        if (!await dbContext.Users.AnyAsync(u => u.Id == userId, cancellationToken)) return Results.NotFound();

        var avatar = await dbContext.UserAvatars.FindAsync([userId], cancellationToken);
        var now = DateTime.UtcNow;
        if (avatar is null)
        {
            dbContext.UserAvatars.Add(new UserAvatar
            {
                UserId = userId,
                Data = image.Value.Data,
                ContentType = image.Value.ContentType,
                UpdatedAt = now,
            });
        }
        else
        {
            avatar.Data = image.Value.Data;
            avatar.ContentType = image.Value.ContentType;
            avatar.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { Message = "Foto de perfil actualizada", AvatarUpdatedAt = now });
    }
}

public class DeleteAvatarHandler(AppDbContext dbContext, ICurrentUserService currentUserService)
    : IRequestHandler<DeleteAvatarRecord, IResult>
{
    public async Task<IResult> Handle(DeleteAvatarRecord request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(currentUserService.GetUserId(), out var userId)) return Results.Unauthorized();

        var avatar = await dbContext.UserAvatars.FindAsync([userId], cancellationToken);
        if (avatar is not null)
        {
            dbContext.UserAvatars.Remove(avatar);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Idempotent: removing a picture that isn't there is still a success.
        return Results.Ok(new { Message = "Foto de perfil eliminada" });
    }
}

public class GetAvatarHandler(AppDbContext dbContext, ICurrentUserService currentUserService)
    : IRequestHandler<GetAvatarRecord, IResult>
{
    public async Task<IResult> Handle(GetAvatarRecord request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(currentUserService.GetUserId(), out var userId)) return Results.Unauthorized();

        var avatar = await dbContext.UserAvatars
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

        return avatar is null
            ? Results.NotFound(new { Message = "No tienes foto de perfil." })
            : Results.File(avatar.Data, avatar.ContentType);
    }
}
