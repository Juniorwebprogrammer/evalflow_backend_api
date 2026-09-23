using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Auth.Resend2FA;

public class Resend2FAHandler(AppDbContext dbContext, IEmailService emailService) : IRequestHandler<Resend2FARecord, IResult>
{
    public async Task<IResult> Handle(Resend2FARecord request, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(request.Email, cancellationToken);
        if (user is not null && user.TwoFactorEnabled)
        {
            await Refresh2FACodeAndSaveAsync(user, cancellationToken);
            await SendEmailChallengeAsync(user, cancellationToken);
        }
        
        return Results.Ok(new { Message = "Si el correo está registrado y el 2FA activado, se ha enviado un nuevo código." });
    }

    private async Task<User?> GetUserAsync(string email, CancellationToken cancellationToken)
    {
        return await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    private async Task Refresh2FACodeAndSaveAsync(User user, CancellationToken cancellationToken)
    {
        user.TwoFactorCode = SecurityUtil.Generate2FACode();
        user.TwoFactorCodeExpiration = DateTime.UtcNow.AddMinutes(10);
        
        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SendEmailChallengeAsync(User user, CancellationToken cancellationToken)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Notifications", "Templates", "TwoFactorEmail.html");
        var template = await File.ReadAllTextAsync(templatePath, cancellationToken);
        
        var emailBody = template
            .Replace("{{UserNombre}}", user.Nombre)
            .Replace("{{TwoFactorCode}}", user.TwoFactorCode);

        await emailService.SendEmailAsync(
            user.Email,
            "Nuevo código de inicio de sesión - EvalFlow",
            emailBody,
            cancellationToken
        );
    }
}