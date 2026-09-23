using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Profile.ChangePassword;

public class ChangePasswordHandler(
    AppDbContext dbContext, 
    ICurrentUserService currentUser, 
    IPasswordHasser passwordHasher) 
    : IRequestHandler<ChangePasswordRecord, IResult>
{
    public async Task<IResult> Handle(ChangePasswordRecord request, CancellationToken cancellationToken)
    {
        var userIdString = currentUser.GetUserId();
        if (string.IsNullOrWhiteSpace(userIdString)) return Results.Unauthorized();
        
        var user = await GetUserAsync(userIdString, cancellationToken);
        if (user is null) return Results.NotFound("Usuario no encontrado.");

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Results.BadRequest("La contraseña actual es incorrecta.");
        }
        
        await SaveNewPasswordAsync(user, request.NewPassword, cancellationToken);
        
        return Results.Ok(new { Message = "Contraseña actualizada correctamente." });
    }

    private async Task<User?> GetUserAsync(string userIdString, CancellationToken cancellationToken)
    {
        if (int.TryParse(userIdString, out int userId))
        {
            return await dbContext.Users.FindAsync([userId], cancellationToken);
        }
        
        return null;
    }

    private async Task SaveNewPasswordAsync(User user, string newPassword, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHasher.Hash(newPassword);
        
        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}