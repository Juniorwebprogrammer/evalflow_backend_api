using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher; 
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Auth.ResetPassword;

public class ResetPasswordHandler(AppDbContext dbContext, IPasswordHasser passwordHasser) : IRequestHandler<ResetPasswordRecord, IResult>
{
    public async Task<IResult> Handle(ResetPasswordRecord request, CancellationToken cancellationToken)
    {
        var user = await GetUserByTokenAsync(request.Token, cancellationToken);

        if (user is null)
        {
            return Results.BadRequest(new { Message = "El enlace de recuperación es inválido o ha caducado." });
        }

        if (HasTokenExpired(user))
        {
            await InvalidateTokenAsync(user, cancellationToken);
            return Results.BadRequest(new { Message = "El enlace de recuperación ha caducado. Por favor, solicita uno nuevo." });
        }

        await UpdatePasswordAndInvalidateTokenAsync(user, request.NewPassword, cancellationToken);

        return Results.Ok(new { Message = "Tu contraseña ha sido actualizada correctamente. Ya puedes iniciar sesión." });
    }

    private async Task<User?> GetUserByTokenAsync(string token, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .FirstOrDefaultAsync(u => u.TokenRecuperacionPassword == token, cancellationToken);
    }

    private static bool HasTokenExpired(User user)
    {
        return !user.ExpiracionTokenRecuperacion.HasValue || user.ExpiracionTokenRecuperacion.Value < DateTime.UtcNow;
    }

    private async Task InvalidateTokenAsync(User user, CancellationToken cancellationToken)
    {
        user.TokenRecuperacionPassword = null;
        user.ExpiracionTokenRecuperacion = null;
        user.FechaActualizacion = DateTime.UtcNow;

        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdatePasswordAndInvalidateTokenAsync(User user, string newPassword, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHasser.Hash(newPassword);
        
        user.TokenRecuperacionPassword = null;
        user.ExpiracionTokenRecuperacion = null;
        user.FechaActualizacion = DateTime.UtcNow;

        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}