using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Frontend;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Security;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Features.Team.InviteEmployee;

public class InviteEmployeeHandler(AppDbContext dbContext, ICurrentUserService currentUser, IPasswordHasser interfacePasswordHasser, IEmailService emailService, IOptions<FrontendSettings> frontendSettings) 
    : IRequestHandler<InviteEmployeeRecord, IResult>
{
    public async Task<IResult> Handle(InviteEmployeeRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.Unauthorized();
        }
        
        var validationError = await ValidateRequestAsync(request, cancellationToken);
        if (validationError is not null) return validationError;
        
        var company = await dbContext.Companies
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);

        if (company is null) return Results.NotFound("La empresa asociada al token no existe.");

        var newEmployee = CreateEmployee(request, company);
        await SaveToDatabaseAsync(newEmployee, cancellationToken);

        await SendInvitationEmailAsync(newEmployee, company.Nombre, cancellationToken);
        
        return GenerateSuccessResponse(newEmployee);
    }

    private async Task<IResult?> ValidateRequestAsync(InviteEmployeeRecord request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains("@"))
            return Results.BadRequest("El email no es válido.");
        
        var validRoles = new[] { AppRoles.Owner, AppRoles.Rrhh, AppRoles.Superior, AppRoles.Employee };
        if (!validRoles.Contains(request.Rol))
            return Results.BadRequest($"El rol '{request.Rol}' no es válido para este entorno.");
        
        var emailExists = await dbContext.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailExists)
            return Results.Conflict("Ya existe un usuario con este correo electrónico en el sistema.");

        return null;
    }

    private User CreateEmployee(InviteEmployeeRecord request, Company company)
    {
        var temporalPassword = Guid.NewGuid().ToString();

        return new User
        {
            Nombre = request.Nombre,
            Apellidos = request.Apellidos,
            Email = request.Email,
            PasswordHash = interfacePasswordHasser.Hash(temporalPassword), 
            Rol = request.Rol,
            Empresa = company,
            FechaCreacion = DateTime.UtcNow,
            EmailVerificado = false,
            TokenInvitacion = Guid.NewGuid().ToString(),
            TokenInvitacionExpiracion = DateTime.UtcNow.AddDays(7)
        };
    }

    private async Task SaveToDatabaseAsync(User user, CancellationToken cancellationToken)
    {
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SendInvitationEmailAsync(User user, string companyName, CancellationToken cancellationToken)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Notifications", "Templates", "InviteEmail.html");
        
        var inviteLink = frontendSettings.Value.BuildLink($"accept-invite?token={user.TokenInvitacion}");

        var emailBody = string.Empty;
        
        if (File.Exists(templatePath))
        {
            var template = await File.ReadAllTextAsync(templatePath, cancellationToken);
            emailBody = template
                .Replace("{{UserNombre}}", user.Nombre)
                .Replace("{{CompanyNombre}}", companyName)
                .Replace("{{InviteLink}}", inviteLink);
        }
        else
        {
            emailBody = $"<h1>Hola {user.Nombre},</h1><p>Te han invitado a unirte a <b>{companyName}</b> en EvalFlow.</p><p>Haz clic <a href='{inviteLink}'>aquí</a> para aceptar la invitación y crear tu contraseña.</p>";
        }
        
        await emailService.SendEmailAsync(
            user.Email,
            $"Invitación a unirte a {companyName} en Evalflow",
            emailBody,
            cancellationToken
        );
    }
    
    private static IResult GenerateSuccessResponse(User user)
    {
        var responsePayload = new InviteEmployeeResponse(
            Message: "Empleado invitado y creado con éxito.",
            UserId: user.Id.ToString(),
            Email: user.Email,
            RolAsignado: user.Rol
        );

        return Results.Created($"/User/get/{user.Id}", responsePayload);
    }
}