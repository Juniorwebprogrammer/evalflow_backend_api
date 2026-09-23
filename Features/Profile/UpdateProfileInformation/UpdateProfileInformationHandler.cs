using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;

namespace evalflow_backend_api.Features.Profile.UpdateProfileInformation;

public class UpdateProfileInformationHandler(AppDbContext dbContext, ICurrentUserService currentUserService) : IRequestHandler<UpdateProfileInformationRecord, IResult>
{
    public async Task<IResult> Handle(UpdateProfileInformationRecord request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
        
        var user = await GetUserEntityAsync(userId, cancellationToken);
        if (user is null) return Results.NotFound("User not found");
        
        await ApplyUpdatesAndSavedAsync(user, request, cancellationToken);
        
        return Results.Ok(new { Message = "Profile updated" });
    }

    private async Task<User?> GetUserEntityAsync(string userIdString, CancellationToken cancellationToken)
    {
        if (int.TryParse(userIdString, out int userId))
        {
            return await dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        }

        return null;
    }

    private async Task ApplyUpdatesAndSavedAsync(User user, UpdateProfileInformationRecord request,
        CancellationToken cancellationToken)
    {
        user.Nombre = request.Nombre;
        user.Apellidos = request.Apellidos;
        user.FechaActualizacion = DateTime.UtcNow;
        
        dbContext.Users.Update(user);
        
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}