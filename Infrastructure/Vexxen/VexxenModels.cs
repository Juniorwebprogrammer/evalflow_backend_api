namespace evalflow_backend_api.Infrastructure.Vexxen;

/// <summary>El usuario del access token (GET /api/auth/userinfo). Vexxen solo deja entrar con el email verificado.</summary>
public record VexxenUser(string Id, string FirstName, string LastName, string Email, bool EmailVerified);

public record VexxenTokens(string AccessToken, string RefreshToken);

public record VexxenAccessPlan(string Id, string Name, bool Paid);

/// <summary>
/// Qué tiene un usuario del producto EvalFlow (GET /api/access/{userId}). Status: active, trialing, canceling,
/// past_due, free o none. Las fechas, en UTC. TransferredTo: sin plan porque se lo transfirió a ese usuario de
/// Vexxen (que ahora lo paga).
/// </summary>
public record VexxenAccess(
    string ProductId,
    bool Access,
    string Status,
    VexxenAccessPlan? Plan,
    string? Cycle,
    DateTime? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    DateTime? TrialEndsAt,
    DateTime? AccessEndsAt,
    string? TransferredTo = null);

/// <summary>Si la cuenta de Vexxen puede usarse (GET /api/access/{userId}: 200, 403 ACCOUNT_SUSPENDED o 404 USER_NOT_FOUND).</summary>
public enum VexxenAccountStatus
{
    Active,
    Suspended,
    Deleted,
}
