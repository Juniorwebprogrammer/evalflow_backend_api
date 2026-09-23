using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Team.AcceptInvite;

public class AcceptInviteHandler(AppDbContext dbContext, IPasswordHasser passwordHasher) 
    : IRequestHandler<AcceptInviteRecord, IResult>
{
    public async Task<IResult> Handle(AcceptInviteRecord request, CancellationToken cancellationToken)
    {
        var user = await GetUserByTokenAsync(request.Token, cancellationToken);

        var validationError = ValidateInvitation(user);
        if (validationError is not null) return validationError;

        await ActivateUserAndSaveAsync(user!, request, cancellationToken);

        return Results.Ok(new { Message = "Invitación aceptada correctamente. Ya puedes iniciar sesión." });
    }

    private async Task<User?> GetUserByTokenAsync(string token, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .FirstOrDefaultAsync(u => u.TokenInvitacion == token, cancellationToken);
    }

    private IResult? ValidateInvitation(User? user)
    {
        if (user is null)
        {
            return Results.BadRequest(new { Message = "El enlace de invitación es inválido o no existe." });
        }

        if (user.TokenInvitacionExpiracion < DateTime.UtcNow)
        {
            return Results.BadRequest(new { Message = "La invitación ha caducado. Solicita a tu administrador que te envíe una nueva." });
        }

        return null; // Todo correcto
    }

    private async Task ActivateUserAndSaveAsync(User user, AcceptInviteRecord request, CancellationToken cancellationToken)
    {
        user.Nombre = request.Nombre;
        user.Apellidos = request.Apellidos;
        user.PasswordHash = passwordHasher.Hash(request.Password);
        user.EmailVerificado = true;
        user.TokenInvitacion = null;
        user.TokenInvitacionExpiracion = null;
        user.FechaActualizacion = DateTime.UtcNow;

        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}