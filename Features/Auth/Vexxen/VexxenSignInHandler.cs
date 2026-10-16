using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Vexxen;
using MediatR;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Features.Auth.Vexxen;

/// <summary>
/// Sign-in with Vexxen. The frontend opens the Vexxen popup (with 2FA if the user has it) and sends the access
/// token here; the refresh token stays in the frontend's cookie. Members get the EvalFlow JWT; people without a
/// company get "needs_onboarding" (and whether they already have a plan).
/// </summary>
public class VexxenSignInHandler(IVexxenClient vexxen, VexxenSessionService sessions, IOptions<VexxenSettings> settings)
    : IRequestHandler<VexxenSignInRecord, IResult>
{
    public const string NeedsOnboarding = "needs_onboarding";

    public async Task<IResult> Handle(VexxenSignInRecord request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken)) return VexxenErrors.InvalidSession();

        try
        {
            var vexxenUser = await vexxen.GetUserInfoAsync(request.AccessToken, cancellationToken);

            var accountError = await sessions.CheckAccountAsync(vexxenUser.Id, StatusCodes.Status403Forbidden, cancellationToken);
            if (accountError is not null) return accountError;

            var (user, error) = await sessions.FindOrLinkAsync(vexxenUser, cancellationToken);
            if (error is not null) return error;

            if (user is null)
            {
                var access = await vexxen.GetAccessAsync(vexxenUser.Id, cancellationToken);
                var hasPlan = access.Access && PlanStatuses.HasAccess(access.Status) && PlanCatalog.FromVexxenPlanId(access.Plan?.Id) is not null;
                return Results.Ok(new NeedsOnboardingResponse(NeedsOnboarding, vexxenUser.Email, vexxenUser.FirstName, vexxenUser.LastName,
                    hasPlan, settings.Value.CatalogUrl));
            }

            var (session, issueError) = await sessions.IssueAsync(user, vexxenUser, refreshToken: null, cancellationToken);
            return issueError ?? Results.Ok(session);
        }
        catch (VexxenUnauthorizedException)
        {
            return VexxenErrors.InvalidSession();
        }
        catch (VexxenAccountSuspendedException)
        {
            return VexxenErrors.Suspended(StatusCodes.Status403Forbidden);
        }
        catch (VexxenException)
        {
            return VexxenErrors.Unavailable();
        }
    }
}

/// <summary>
/// Renews the session at Vexxen (so a password change, a suspension or a deleted account there ends it here too) and
/// issues a new EvalFlow JWT with the plan re-read. Returns the new Vexxen refresh token: the old one no longer works.
/// </summary>
public class RefreshSessionHandler(IVexxenClient vexxen, VexxenSessionService sessions, ILogger<RefreshSessionHandler> logger)
    : IRequestHandler<RefreshSessionRecord, IResult>
{
    public async Task<IResult> Handle(RefreshSessionRecord request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return VexxenErrors.InvalidSession();

        try
        {
            var tokens = await vexxen.RefreshAsync(request.RefreshToken, cancellationToken);
            var vexxenUser = await vexxen.GetUserInfoAsync(tokens.AccessToken, cancellationToken);

            // Suspended or deleted since the last refresh: the session ends here, and the refresh token Vexxen has just
            // issued is revoked (the one the frontend sent is already spent).
            var accountError = await sessions.CheckAccountAsync(vexxenUser.Id, StatusCodes.Status401Unauthorized, cancellationToken);
            if (accountError is not null)
            {
                await RevokeAsync(tokens.RefreshToken);
                return accountError;
            }

            var (user, error) = await sessions.FindOrLinkAsync(vexxenUser, cancellationToken);
            if (error is not null) return error;
            if (user is null)
            {
                // Removed from the company (or the company was deleted) since they signed in.
                return VexxenErrors.Error(StatusCodes.Status401Unauthorized, "NOT_A_MEMBER", "Ya no perteneces a ninguna empresa en EvalFlow.");
            }

            var (session, issueError) = await sessions.IssueAsync(user, vexxenUser, tokens.RefreshToken, cancellationToken);
            return issueError ?? Results.Ok(session);
        }
        catch (VexxenUnauthorizedException)
        {
            return VexxenErrors.InvalidSession();
        }
        catch (VexxenAccountSuspendedException)
        {
            // Vexxen itself refused to renew it: there's no new refresh token to revoke.
            return VexxenErrors.Suspended(StatusCodes.Status401Unauthorized);
        }
        catch (VexxenException)
        {
            return VexxenErrors.Unavailable();
        }
    }

    // Best effort: if Vexxen doesn't answer, the token stays unused in the frontend's cookie, and the next refresh with
    // it is checked again.
    private async Task RevokeAsync(string refreshToken)
    {
        try
        {
            await vexxen.LogoutAsync(refreshToken, CancellationToken.None);
        }
        catch (VexxenException ex)
        {
            logger.LogWarning(ex, "Couldn't revoke the Vexxen refresh token of a suspended or deleted account");
        }
    }
}

/// <summary>Signs out: revokes the Vexxen refresh token. Always 204 (the frontend clears its cookies anyway).</summary>
public class LogoutHandler(IVexxenClient vexxen, ILogger<LogoutHandler> logger) : IRequestHandler<LogoutRecord, IResult>
{
    public async Task<IResult> Handle(LogoutRecord request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            try
            {
                await vexxen.LogoutAsync(request.RefreshToken, cancellationToken);
            }
            catch (VexxenException ex)
            {
                logger.LogWarning(ex, "Couldn't revoke the Vexxen session on logout");
            }
        }

        return Results.NoContent();
    }
}
