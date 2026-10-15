using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Admin.CreateAdministrator;

public record CreateAdministratorResult(bool Ok, string Message, DateTime? InvitationExpiresAt = null);

/// <summary>
/// Creates a global Administrator (EvalFlow staff): they change any company's plan (Enterprise), and read-only
/// doesn't apply to them. Every user needs a company, so administrators live in an internal one (Enterprise,
/// manual plan, no billing user). Like an invitation, the account gets linked the first time the person signs in
/// to EvalFlow with Vexxen using that email (they need a Vexxen account with it).
///
/// Run: dotnet run -- create-admin &lt;email&gt; &lt;first name&gt; &lt;last name&gt;
/// (with ConnectionStrings__DefaultConnection pointing at the database: local or Neon).
/// </summary>
public class CreateAdministratorCommand(AppDbContext dbContext)
{
    public const string InternalCompanyId = "evalflow-internal";
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(30);

    public async Task<CreateAdministratorResult> ExecuteAsync(string email, string nombre, string apellidos, CancellationToken cancellationToken,
        DateTime? now = null)
    {
        var at = now ?? DateTime.UtcNow;
        email = email.Trim().ToLowerInvariant();
        if (!email.Contains('@')) return new(false, $"\"{email}\" isn't a valid email.");

        var existing = await dbContext.Users.Include(u => u.Empresa)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, cancellationToken);

        if (existing is not null && existing.Rol != AppRoles.Administrator)
        {
            return new(false, $"{email} already belongs to the company \"{existing.Empresa?.Nombre}\" as {existing.Rol}. " +
                              "An email can only be in one place in EvalFlow.");
        }

        if (existing is not null && existing.VexxenUserId is not null)
        {
            return new(true, $"{email} is already an Administrator and has signed in with Vexxen: nothing to do.");
        }

        var company = await GetOrCreateInternalCompanyAsync(at, cancellationToken);
        var admin = existing ?? new User
        {
            Nombre = Truncate(nombre, 40),
            Apellidos = Truncate(apellidos, 40),
            Email = email,
            Rol = AppRoles.Administrator,
            Empresa = company,
            FechaCreacion = at,
        };
        if (existing is null) dbContext.Users.Add(admin);

        // A new invitation each time: re-running the command renews an expired one.
        admin.TokenInvitacion = Guid.NewGuid().ToString();
        admin.TokenInvitacionExpiracion = at + InvitationLifetime;
        admin.Activo = true;
        admin.FechaActualizacion = existing is null ? null : at;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new(true,
            $"{email} is now an EvalFlow Administrator. They must sign in to EvalFlow with Vexxen using that email " +
            $"before {admin.TokenInvitacionExpiracion:yyyy-MM-dd} (UTC); run this again to renew it.",
            admin.TokenInvitacionExpiracion);
    }

    private async Task<Company> GetOrCreateInternalCompanyAsync(DateTime at, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.FirstOrDefaultAsync(c => c.IdentificationId == InternalCompanyId, cancellationToken);
        if (company is not null) return company;

        company = new Company
        {
            Nombre = "evalflow",
            Colors = "#2563eb",
            IdentificationId = InternalCompanyId,
            PlanId = PlanCatalog.Enterprise,
            PlanStatus = PlanStatuses.Active,
            PlanManual = true,
            Cif = "INTERNAL",
            Sector = "Internal",
            FechaCreacion = at,
        };
        dbContext.Companies.Add(company);
        return company;
    }

    private static string Truncate(string value, int max)
    {
        value = value.Trim();
        return value.Length > max ? value[..max] : value;
    }
}

/// <summary>`dotnet run -- create-admin ...`: runs the command instead of the API.</summary>
public static class CreateAdministratorCli
{
    public const string Command = "create-admin";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: dotnet run -- create-admin <email> <first name> <last name>");
            return 1;
        }

        using var scope = services.CreateScope();
        var command = new CreateAdministratorCommand(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        try
        {
            var result = await command.ExecuteAsync(args[1], args[2], string.Join(' ', args[3..]), CancellationToken.None);
            (result.Ok ? Console.Out : Console.Error).WriteLine(result.Message);
            return result.Ok ? 0 : 1;
        }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException or Npgsql.NpgsqlException)
        {
            // Usually the database isn't reachable (ConnectionStrings__DefaultConnection) or isn't migrated yet.
            Console.Error.WriteLine($"Couldn't create the administrator: {(ex.InnerException ?? ex).Message}");
            return 2;
        }
    }
}
