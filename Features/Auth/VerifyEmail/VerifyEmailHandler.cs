using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Auth.VerifyEmail;

public class VerifyEmailHandler(AppDbContext dbContext) : IRequestHandler<VerifyEmailRecord, IResult>
{
    public async Task<IResult> Handle(VerifyEmailRecord request, CancellationToken cancellationToken)
    {
        var user = await GetUserWithCompanyByTokenAsync(request.Token, cancellationToken);

        if (user is null)
        {
            return Results.BadRequest(new { Message = "El enlace de verificación es inválido." });
        }

        if (HasTokenExpired(user))
        {
            return Results.BadRequest(new { Message = "El enlace ha caducado. Por favor, solicita uno nuevo." });
        }

        if (user.EmailVerificado)
        {
            return Results.Ok(new { 
                Message = "El correo ya había sido verificado anteriormente.",
                TenantId = user.Empresa?.IdentificationId 
            });
        }
        
        await MarkEmailAsVerifiedAsync(user, cancellationToken);
        
        return Results.Ok(new 
        { 
            Message = "Correo verificado con éxito.",
            TenantId = user.Empresa?.IdentificationId
        });
    }

    private async Task<User?> GetUserWithCompanyByTokenAsync(string token, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .Include(u => u.Empresa)
            .FirstOrDefaultAsync(u => u.TokenValidacionEmail == token, cancellationToken);
    }

    private static bool HasTokenExpired(User user)
    {
        return user.TokenValidacionExpiracion.HasValue && user.TokenValidacionExpiracion.Value < DateTime.UtcNow;
    }

    private async Task MarkEmailAsVerifiedAsync(User user, CancellationToken cancellationToken)
    {
        user.EmailVerificado = true;
        user.TokenValidacionEmail = null;
        user.TokenValidacionExpiracion = null;
        user.FechaActualizacion = DateTime.UtcNow;

        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}