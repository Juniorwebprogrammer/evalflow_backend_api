using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Frontend;
using evalflow_backend_api.Infrastructure.Notifications;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Features.Auth.ForgotPassword;

public class ForgotPasswordHandler(AppDbContext dbContext, IEmailService emailService, IOptions<FrontendSettings> frontendSettings) : IRequestHandler<ForgotPasswordRecord, IResult>
{
    public async Task<IResult> Handle(ForgotPasswordRecord request, CancellationToken cancellationToken)
    {
        var user = await GetUserByEmailAsync(request.Email, cancellationToken);
        
        if (user is not null && user.Activo)
        {
            await GenerateTokenAndSaveAsync(user, cancellationToken);
            await SendRecoveryEmailAsync(user, cancellationToken);
        }
        
        return Results.Ok(new { Message = "Si el correo electrónico existe en nuestro sistema, recibirás un enlace para restablecer tu contraseña." });
    }

    private async Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
    }

    private async Task GenerateTokenAndSaveAsync(User user, CancellationToken cancellationToken)
    {
        user.TokenRecuperacionPassword = Guid.NewGuid().ToString();
        user.ExpiracionTokenRecuperacion = DateTime.UtcNow.AddHours(1);
        user.FechaActualizacion = DateTime.UtcNow;

        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SendRecoveryEmailAsync(User user, CancellationToken cancellationToken)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Notifications", "Templates", "PasswordRecovery.html");
        
        if (!File.Exists(templatePath)) return;

        var template = await File.ReadAllTextAsync(templatePath, cancellationToken);
        
        var recoveryLink = frontendSettings.Value.BuildLink($"reset-password?token={user.TokenRecuperacionPassword}");

        var emailBody = template
            .Replace("{{UserNombre}}", user.Nombre)
            .Replace("{{RecoveryLink}}", recoveryLink)
            .Replace("{{Year}}", DateTime.UtcNow.Year.ToString());

        await emailService.SendEmailAsync(
            user.Email,
            "Restablecer contraseña - EvalFlow",
            emailBody,
            cancellationToken
        );
    }
}