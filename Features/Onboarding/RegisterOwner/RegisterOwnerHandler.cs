using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Frontend;
using evalflow_backend_api.Infrastructure.Database.Seeders;
using evalflow_backend_api.Infrastructure.Jwt;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Features.Onboarding.RegisterOwner;

public class RegisterOwnerHandler(AppDbContext dbContext, IJwtProvider interfaceJwtProvider, IPasswordHasser interfacePasswordHasser, IEmailService emailService, IOptions<FrontendSettings> frontendSettings) : IRequestHandler<RegisterOwnerRecord, IResult>
{
    public async Task<IResult> Handle(RegisterOwnerRecord request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateRequestAsync(request, cancellationToken);
        if (validationError is not null) return validationError;
        
        var (newUser, newCompany) = CreateEntities(request);

        await SaveToDatabaseAsync(newUser, cancellationToken);

        // Provisiona las plantillas por defecto (180°, 360° y autoevaluación) para el nuevo Tenant.
        await DefaultTemplatesSeeder.SeedForCompanyAsync(dbContext, newCompany.Id, cancellationToken);

        var (jwt, refreshToken) = GenerateTokens(newUser, newCompany);

        if (newUser.TokenValidacionEmail == null) return GenerateSuccessResponse(newUser, jwt, refreshToken);
        var emailBody = await GenerateWelcomeEmailHtmlAsync(request.UserNombre, request.CompanyNombre, newUser.TokenValidacionEmail);

        await emailService.SendEmailAsync(
            request.Email,
            "Verifica tu cuenta en Evalflow",
            emailBody,
            cancellationToken
        );

        return GenerateSuccessResponse(newUser, jwt, refreshToken);
    }

    private async Task<IResult?> ValidateRequestAsync(RegisterOwnerRecord request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains("@"))
            return Results.BadRequest("El email no es válido.");
        
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return Results.BadRequest("La contraseña debe tener al menos 6 caracteres.");
        
        var emailExists = await dbContext.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailExists)
            return Results.Conflict("Ya existe un usuario registrado con este correo electrónico.");

        return null;
    }
    
    private (User, Company) CreateEntities(RegisterOwnerRecord request)
    {
        var company = new Company
        {
            Nombre = request.CompanyNombre.ToLower(),
            Colors = request.CompanyColors,
            PlanId = request.PlanId,
            IdentificationId = Guid.NewGuid().ToString("N"),
            Cif = request.Cif,
            DireccionFiscal = request.DireccionFiscal,
            Sector = request.Sector,
            FechaCreacion = DateTime.UtcNow
        };
        
        var user = new User
        {
            Nombre = request.UserNombre,
            Apellidos = request.Apellidos,
            Email = request.Email,
            PasswordHash = interfacePasswordHasser.Hash(request.Password), 
            Empresa = company, 
            Rol = AppRoles.Owner,
            FechaCreacion = DateTime.UtcNow,
            EmailVerificado = false,
            TokenValidacionEmail = Guid.NewGuid().ToString("N"),
            TokenValidacionExpiracion = DateTime.UtcNow.AddHours(24)
        };

        return (user, company);
    }

    private async Task SaveToDatabaseAsync(User user, CancellationToken cancellationToken)
    {
        dbContext.Users.Add(user);
        
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private (string Jwt, string RefreshToken) GenerateTokens(User user, Company company)
    {
        var jwt = interfaceJwtProvider.GenerateJwt(user, company);
        
        var refreshToken = interfaceJwtProvider.GenerateRefreshToken();
        
        return (jwt, refreshToken);
    }

    private async Task<string> GenerateWelcomeEmailHtmlAsync(string userNombre, string companyNombre, string token)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Notifications", "Templates", "WelcomeEmail.html");
        var template = await File.ReadAllTextAsync(templatePath);
        var validationLink = frontendSettings.Value.BuildLink($"auth/verify-email?token={token}");
        var currentYear = DateTime.UtcNow.Year.ToString();
        
        return template
            .Replace("{{UserNombre}}", userNombre)
            .Replace("{{CompanyNombre}}", companyNombre)
            .Replace("{{ValidationLink}}", validationLink)
            .Replace("{{Year}}", currentYear);
    }
    
    private IResult GenerateSuccessResponse(User user, string jwt, string refreshToken)
    {
        var responsePayload = new RegisterOwnerResponse(
            Message: "Usuario y Empresa creados con éxito.",
            UserNombre: user.Nombre,
            Jwt: jwt,
            RefreshToken: refreshToken
        );
        
        return Results.Ok(responsePayload);
    }
}