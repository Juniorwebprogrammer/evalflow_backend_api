using evalflow_backend_api.Infrastructure.Vexxen;

namespace evalflow_backend_api.Tests.Common;

/// <summary>
/// Vexxen in memory: users by access token, plans by Vexxen user id and refresh tokens that rotate. Set Down to
/// simulate Vexxen being unavailable (AccessDown: only GET /api/access). SuspendedUserIds: accounts suspended from
/// Vexxen Administrator (/api/access answers ACCOUNT_SUSPENDED; the refresh too if RefreshChecksSuspension).
/// </summary>
public class FakeVexxenClient : IVexxenClient
{
    public Dictionary<string, VexxenUser> UsersByAccessToken { get; } = new();
    public Dictionary<string, VexxenAccess> AccessByUserId { get; } = new();
    /// <summary>refresh token → Vexxen user id.</summary>
    public Dictionary<string, string> RefreshTokens { get; } = new();
    public HashSet<string> DeletedUserIds { get; } = new();
    public HashSet<string> SuspendedUserIds { get; } = new();
    public bool RefreshChecksSuspension { get; set; }
    public bool AccessDown { get; set; }
    public int AccountStatusCalls { get; private set; }
    public List<string> LoggedOut { get; } = new();
    public int AccessCalls { get; private set; }
    public bool Down { get; set; }

    /// <summary>A Vexxen user with a valid access token (returned) and, optionally, an EvalFlow plan.</summary>
    public string AddUser(VexxenUser user, string? planId = null, string status = "active")
    {
        var accessToken = $"access-{user.Id}-{Guid.NewGuid():N}";
        UsersByAccessToken[accessToken] = user;
        if (planId is not null) SetPlan(user.Id, planId, status);
        return accessToken;
    }

    public string AddRefreshToken(string userId)
    {
        var token = $"refresh-{userId}-{Guid.NewGuid():N}";
        RefreshTokens[token] = userId;
        return token;
    }

    public void SetPlan(string userId, string planId, string status = "active", DateTime? trialEndsAt = null, DateTime? accessEndsAt = null) =>
        AccessByUserId[userId] = Access(planId, status, trialEndsAt, accessEndsAt);

    public static VexxenAccess Access(string? planId, string status = "active", DateTime? trialEndsAt = null, DateTime? accessEndsAt = null) =>
        new("evaluation-flow", status is "active" or "trialing" or "canceling" or "past_due", status,
            planId is null ? null : new VexxenAccessPlan(planId, planId, true), "monthly", DateTime.UtcNow.AddDays(30), status == "canceling",
            trialEndsAt, accessEndsAt);

    public static VexxenUser User(string id, string email, string firstName = "Ada", string lastName = "Lovelace") =>
        new(id, firstName, lastName, email, true);

    public Task<VexxenUser> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken)
    {
        ThrowIfDown();
        if (!UsersByAccessToken.TryGetValue(accessToken, out var user) || DeletedUserIds.Contains(user.Id))
            throw new VexxenUnauthorizedException("invalid token");
        return Task.FromResult(user);
    }

    public Task<VexxenAccess> GetAccessAsync(string vexxenUserId, CancellationToken cancellationToken)
    {
        ThrowIfDown(AccessDown);
        AccessCalls++;
        if (DeletedUserIds.Contains(vexxenUserId)) throw new VexxenUserNotFoundException(vexxenUserId);
        if (SuspendedUserIds.Contains(vexxenUserId)) throw new VexxenAccountSuspendedException(vexxenUserId);
        return Task.FromResult(AccessByUserId.TryGetValue(vexxenUserId, out var access) ? access : Access(null, "none"));
    }

    public Task<VexxenAccountStatus> GetAccountStatusAsync(string vexxenUserId, CancellationToken cancellationToken)
    {
        ThrowIfDown(AccessDown);
        AccountStatusCalls++;
        return Task.FromResult(DeletedUserIds.Contains(vexxenUserId) ? VexxenAccountStatus.Deleted
            : SuspendedUserIds.Contains(vexxenUserId) ? VexxenAccountStatus.Suspended
            : VexxenAccountStatus.Active);
    }

    public Task<VexxenTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        ThrowIfDown();
        if (!RefreshTokens.Remove(refreshToken, out var userId) || DeletedUserIds.Contains(userId))
            throw new VexxenUnauthorizedException("expired");
        if (RefreshChecksSuspension && SuspendedUserIds.Contains(userId)) throw new VexxenAccountSuspendedException();
        var user = UsersByAccessToken.Values.First(u => u.Id == userId);
        var accessToken = $"access-{userId}-{Guid.NewGuid():N}";
        UsersByAccessToken[accessToken] = user;
        return Task.FromResult(new VexxenTokens(accessToken, AddRefreshToken(userId)));
    }

    public Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        ThrowIfDown();
        RefreshTokens.Remove(refreshToken);
        LoggedOut.Add(refreshToken);
        return Task.CompletedTask;
    }

    private void ThrowIfDown(bool alsoIf = false)
    {
        if (Down || alsoIf) throw new VexxenUnavailableException("Vexxen is down");
    }
}
