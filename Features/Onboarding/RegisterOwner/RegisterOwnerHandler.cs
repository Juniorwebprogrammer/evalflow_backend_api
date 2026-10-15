using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Auth.Vexxen;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Database.Seeders;
using evalflow_backend_api.Infrastructure.Jwt;
using evalflow_backend_api.Infrastructure.Plans;
using evalflow_backend_api.Infrastructure.Vexxen;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Features.Onboarding.RegisterOwner;

public class RegisterOwnerHandler(AppDbContext dbContext, IJwtProvider jwtProvider, IVexxenClient vexxen, IOptions<VexxenSettings> vexxenSettings)
    : IRequestHandler<RegisterOwnerRecord, IResult>
{
    public async Task<IResult> Handle(RegisterOwnerRecord request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyNombre))
            return Results.BadRequest(new { Message = "El nombre de la empresa es obligatorio." });
        if (string.IsNullOrWhiteSpace(request.VexxenAccessToken)) return VexxenErrors.InvalidSession();

        VexxenUser vexxenUser;
        VexxenAccess access;
        try
        {
            vexxenUser = await vexxen.GetUserInfoAsync(request.VexxenAccessToken, cancellationToken);
            access = await vexxen.GetAccessAsync(vexxenUser.Id, cancellationToken);
        }
        catch (VexxenUnauthorizedException)
        {
            return VexxenErrors.InvalidSession();
        }
        catch (VexxenException)
        {
            return VexxenErrors.Unavailable();
        }

        var conflict = await CheckConflictsAsync(vexxenUser, cancellationToken);
        if (conflict is not null) return conflict;

        if (!access.Access || !PlanStatuses.HasAccess(access.Status) || PlanCatalog.FromVexxenPlanId(access.Plan?.Id) is null)
        {
            return Results.Json(new
            {
                Code = "NO_PLAN",
                Message = "Para crear tu empresa necesitas un plan de EvalFlow. Elige uno en Vexxen (incluye 14 días de prueba).",
                vexxenSettings.Value.CatalogUrl,
            }, statusCode: StatusCodes.Status409Conflict);
        }

        var (owner, company) = CreateEntities(request, vexxenUser, access);
        dbContext.Users.Add(owner);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Provisiona las plantillas por defecto (180°, 360° y autoevaluación) para el nuevo Tenant.
        await DefaultTemplatesSeeder.SeedForCompanyAsync(dbContext, company.Id, cancellationToken);

        return Results.Ok(new RegisterOwnerResponse(
            Message: "Usuario y Empresa creados con éxito.",
            UserNombre: owner.Nombre,
            Jwt: jwtProvider.GenerateJwt(owner, company),
            IdentificationId: company.IdentificationId,
            PlanId: company.PlanId,
            PlanStatus: company.PlanStatus));
    }

    // One company per person (the email is unique across EvalFlow) and one company per Vexxen subscription.
    private async Task<IResult?> CheckConflictsAsync(VexxenUser vexxenUser, CancellationToken cancellationToken)
    {
        var email = vexxenUser.Email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(u => u.VexxenUserId == vexxenUser.Id || u.Email.ToLower() == email, cancellationToken))
        {
            return VexxenErrors.Error(StatusCodes.Status409Conflict, "ALREADY_MEMBER",
                "Tu cuenta ya pertenece a una empresa en EvalFlow (o tienes una invitación pendiente): inicia sesión.");
        }

        if (await dbContext.Companies.AnyAsync(c => c.BillingVexxenUserId == vexxenUser.Id, cancellationToken))
        {
            return VexxenErrors.Error(StatusCodes.Status409Conflict, "SUBSCRIPTION_IN_USE", "Tu suscripción de EvalFlow ya está asociada a otra empresa.");
        }

        return null;
    }

    private static (User, Company) CreateEntities(RegisterOwnerRecord request, VexxenUser vexxenUser, VexxenAccess access)
    {
        var company = new Company
        {
            Nombre = request.CompanyNombre.Trim().ToLower(),
            Colors = request.CompanyColors,
            PlanId = PlanCatalog.Starter,
            BillingVexxenUserId = vexxenUser.Id,
            IdentificationId = Guid.NewGuid().ToString("N"),
            Cif = request.Cif,
            DireccionFiscal = request.DireccionFiscal,
            Sector = request.Sector,
            FechaCreacion = DateTime.UtcNow
        };
        PlanSync.Apply(company, access, DateTime.UtcNow);

        var owner = new User
        {
            Nombre = Truncate(vexxenUser.FirstName, 40),
            Apellidos = Truncate(vexxenUser.LastName, 40),
            Email = vexxenUser.Email.Trim().ToLowerInvariant(),
            VexxenUserId = vexxenUser.Id,
            Empresa = company,
            Rol = AppRoles.Owner,
            FechaCreacion = DateTime.UtcNow,
        };

        return (owner, company);
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}
