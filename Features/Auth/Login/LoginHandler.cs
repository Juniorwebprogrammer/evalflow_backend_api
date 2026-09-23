using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Jwt;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Security;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Auth.Login;

public class LoginHandler(AppDbContext dbContext, IJwtProvider jwtProvider, IPasswordHasser interfacePasswordHasser, IEmailService emailService) : IRequestHandler<LoginRecord, IResult>
{
    public async Task<IResult> Handle(LoginRecord request, CancellationToken cancellationToken)
    {
        var user = await GetUserWithCompanyAsync(request.Email, cancellationToken);
        
        var validationError = ValidateCredentialsAndTenant(user, request.Password, request.IdentificationId);
        
        if (validationError is not null) return validationError;

        if (user!.TwoFactorEnabled)
        {
            await ProcessTwoFactorChallengeAsync(user, cancellationToken);

            return Results.Ok(new
            {
                Requires2FA = true,
                user.Email,
                Message = "Código 2FA enviado al correo."
            });
        }

        var (jwt, refreshToken) = GenerateTokens(user, user.Empresa!);
        
        return GenerateSuccessResponse(user, jwt, refreshToken);
    }

    private async Task<User?> GetUserWithCompanyAsync(string email, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .Include(user => user.Empresa)
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    private IResult? ValidateCredentialsAndTenant(User? user, string password, string identificationId)
    {
        if (user is null || !interfacePasswordHasser.Verify(password, user.PasswordHash) || user.Empresa?.IdentificationId != identificationId)
        {
            return Results.Unauthorized();
        }

        if (!user.EmailVerificado)
        {
            return Results.BadRequest(new
            {
                Message =
                    "Debes verificar tu correo electrónico antes de iniciar sesión. Por favor, revisa tu bandeja de entrada."
            });
        }

        return null;
    }

    private (string jwt, string refreshToken) GenerateTokens(User user, Company company)
    {
        var jwt = jwtProvider.GenerateJwt(user, company);

        var refreshToken = jwtProvider.GenerateRefreshToken();
        
        return (jwt, refreshToken);
    }

    private async Task ProcessTwoFactorChallengeAsync(User user, CancellationToken cancellationToken)
    {
        user.TwoFactorCode = SecurityUtil.Generate2FACode();
        user.TwoFactorCodeExpiration = DateTime.UtcNow.AddMinutes(10);

        dbContext.Users.Update(user);
        
        await dbContext.SaveChangesAsync(cancellationToken);
        
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Notifications", "Templates", "TwoFactorEmail.html");
        var template = await File.ReadAllTextAsync(templatePath, cancellationToken);

        var emailBody = template
            .Replace("{{UserNombre}}", user.Nombre)
            .Replace("{{TwoFactorCode}}", user.TwoFactorCode);
        
        await emailService.SendEmailAsync(
            user.Email,
            "Código de inicio de sesión - EvalFlow",
            emailBody,
            cancellationToken
        );
    }
    
    private static IResult GenerateSuccessResponse(User user, string jwt, string refreshToken)
    {
        var responsePayload = new LoginResponse(
            Message: "Inicio de sesión exitoso",
            Username: user.Nombre,
            Jwt: jwt,
            RefreshToken: refreshToken,
            TwoFactorEnabled: user.TwoFactorEnabled
        );
        
        return Results.Ok(responsePayload);
    }
}