using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Jwt;
using evalflow_backend_api.Infrastructure.Plans;
using evalflow_backend_api.Infrastructure.Vexxen;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Auth.Vexxen;

/// <summary>The EvalFlow session that follows a valid Vexxen session.</summary>
public record VexxenSessionResponse(
    string Status,
    string Jwt,
    string? RefreshToken,
    string Username,
    string Rol,
    string IdentificationId,
    string PlanStatus);

/// <summary>
/// What sign-in and refresh share: find the EvalFlow user of a Vexxen account (linking a pending invitation the
/// first time), copy their identity from Vexxen, sync the company plan and issue the usual EvalFlow JWT
/// (sub, email, identificationId, role).
/// </summary>
public class VexxenSessionService(AppDbContext dbContext, IJwtProvider jwtProvider, IPlanSyncService planSync)
{
    public const string SignedIn = "signed_in";

    /// <summary>
    /// The user linked to this Vexxen account. If there's none, an invited user with the same email (Vexxen only
    /// signs in verified emails) gets linked now. Null if the person isn't in any company yet (onboarding).
    /// </summary>
    public async Task<(User? User, IResult? Error)> FindOrLinkAsync(VexxenUser vexxenUser, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.Include(u => u.Empresa)
            .FirstOrDefaultAsync(u => u.VexxenUserId == vexxenUser.Id, cancellationToken);
        if (user is not null) return (user, null);

        var email = vexxenUser.Email.Trim().ToLowerInvariant();
        var invited = await dbContext.Users.Include(u => u.Empresa)
            .FirstOrDefaultAsync(u => u.VexxenUserId == null && u.Email.ToLower() == email, cancellationToken);
        if (invited is null) return (null, null);

        // Every user without a Vexxen account was invited: an owner always comes from Vexxen.
        if (invited.TokenInvitacion is null || invited.TokenInvitacionExpiracion < DateTime.UtcNow)
        {
            return (null, VexxenErrors.Error(StatusCodes.Status403Forbidden, "INVITATION_EXPIRED",
                "Tu invitación a EvalFlow ha caducado. Pide a tu empresa que te envíe otra."));
        }

        invited.VexxenUserId = vexxenUser.Id;
        invited.TokenInvitacion = null;
        invited.TokenInvitacionExpiracion = null;
        invited.FechaActualizacion = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return (invited, null);
    }

    /// <summary>The EvalFlow session for <paramref name="user"/>, or why they can't have one (disabled account).</summary>
    public async Task<(VexxenSessionResponse? Session, IResult? Error)> IssueAsync(User user, VexxenUser vexxenUser, string? refreshToken,
        CancellationToken cancellationToken)
    {
        if (!user.Activo)
        {
            return (null, VexxenErrors.Error(StatusCodes.Status403Forbidden, "USER_INACTIVE",
                "Tu cuenta de EvalFlow está desactivada. Habla con el administrador de tu empresa."));
        }

        await CopyIdentityAsync(user, vexxenUser, cancellationToken);

        var company = user.Empresa ?? await dbContext.Companies.FirstAsync(c => c.Id == user.EmpresaID, cancellationToken);
        await planSync.SyncAsync(company, cancellationToken);

        var jwt = jwtProvider.GenerateJwt(user, company);
        return (new VexxenSessionResponse(SignedIn, jwt, refreshToken, user.Nombre, user.Rol, company.IdentificationId, company.PlanStatus), null);
    }

    // Vexxen owns the name and the email: EvalFlow keeps a copy for listings, emails and PDFs.
    private async Task CopyIdentityAsync(User user, VexxenUser vexxenUser, CancellationToken cancellationToken)
    {
        var email = vexxenUser.Email.Trim().ToLowerInvariant();
        var changed = false;
        if (!string.IsNullOrWhiteSpace(vexxenUser.FirstName) && user.Nombre != vexxenUser.FirstName)
        {
            user.Nombre = Truncate(vexxenUser.FirstName, 40);
            changed = true;
        }
        if (user.Apellidos != vexxenUser.LastName)
        {
            user.Apellidos = Truncate(vexxenUser.LastName, 40);
            changed = true;
        }
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)
            && !await dbContext.Users.AnyAsync(u => u.Id != user.Id && u.Email.ToLower() == email, cancellationToken))
        {
            user.Email = email;
            changed = true;
        }

        if (!changed) return;
        user.FechaActualizacion = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}
