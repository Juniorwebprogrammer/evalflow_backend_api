using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Auth.ResendVerification;

public class ResendVerificationHandler(AppDbContext dbContext, IEmailService emailService) : IRequestHandler<ResendVerificationRecord, IResult>
{
    public async Task<IResult> Handle(ResendVerificationRecord request, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(request.Email, cancellationToken);
        if (user is not null && !user.EmailVerificado)
        {
            await RefreshVerificationTokenAndSaveAsync(user, cancellationToken);
            await SendWelcomeEmailAsync(user, cancellationToken);
        }
        
        return Results.Ok(new { Message = "Si el correo está registrado y pendiente de verificación, se ha enviado un nuevo enlace." });
    }

    private async Task<User?> GetUserAsync(string email, CancellationToken cancellationToken)
    {
        return await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    private async Task RefreshVerificationTokenAndSaveAsync(User user, CancellationToken cancellationToken)
    {
        user.TokenValidacionEmail = Guid.NewGuid().ToString(); 
        user.TokenValidacionExpiracion = DateTime.UtcNow.AddHours(24);
        user.FechaActualizacion = DateTime.UtcNow; 

        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SendWelcomeEmailAsync(User user, CancellationToken cancellationToken)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Notifications", "Templates", "WelcomeEmail.html");
        
        if (!File.Exists(templatePath)) return; 

        var template = await File.ReadAllTextAsync(templatePath, cancellationToken);
        
        var verificationLink = $"http://localhost:3000/auth/verify-email?token={user.TokenValidacionEmail}";

        var emailBody = template
            .Replace("{{UserNombre}}", user.Nombre)
            .Replace("{{ValidationLink}}", verificationLink);

        await emailService.SendEmailAsync(
            user.Email,
            "Reenvío: Verifica tu cuenta - EvalFlow",
            emailBody,
            cancellationToken
        );
    }
}