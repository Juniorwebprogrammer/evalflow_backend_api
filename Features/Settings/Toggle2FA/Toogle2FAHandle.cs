using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using MediatR;

namespace evalflow_backend_api.Features.Settings.Toggle2FA;

public class Toogle2FAHandle(AppDbContext dbContext) : IRequestHandler<Toggle2FARecord, IResult>
{
    public async Task<IResult> Handle(Toggle2FARecord request, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Results.NotFound(new { Message = "Usuario no encontrado" });
        }

        await Apply2FASettingsAndSaveAsync(user, request.Enable, cancellationToken);

        return GenerateSuccessResponse(request.Enable);
    }

    private async Task<User?> GetUserAsync(int userId, CancellationToken cancellationToken)
    {
        return await dbContext.Users.FindAsync(new object?[] { userId }, cancellationToken);
    }

    private async Task Apply2FASettingsAndSaveAsync(User user, bool enable2FA, CancellationToken cancellationToken)
    {
        user.TwoFactorEnabled = enable2FA;

        if (!enable2FA)
        {
            user.TwoFactorCode = null;
            user.TokenValidacionExpiracion = null;
        }

        user.FechaActualizacion = DateTime.UtcNow;
        
        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IResult GenerateSuccessResponse(bool isEnabled)
    {
        var status = isEnabled ? "activada" : "desactivada";

        return Results.Ok(new { Message = $"Autenticación de dos factores {status} correctamente" });
    }
}