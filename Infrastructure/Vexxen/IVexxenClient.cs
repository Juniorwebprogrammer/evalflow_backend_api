namespace evalflow_backend_api.Infrastructure.Vexxen;

/// <summary>
/// La API de Vexxen, como la usa @vexxen/auth: cabeceras X-Client-Id / X-Api-Key y, para el usuario, su access token.
/// Lanza VexxenUnauthorizedException, VexxenAccountSuspendedException, VexxenUserNotFoundException o
/// VexxenUnavailableException.
/// </summary>
public interface IVexxenClient
{
    Task<VexxenUser> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken);

    /// <summary>El acceso del usuario al producto de EvalFlow (su plan).</summary>
    Task<VexxenAccess> GetAccessAsync(string vexxenUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Si la cuenta sigue activa, está suspendida o se borró (el mismo GET /api/access/{userId}). Si Vexxen no
    /// contesta o falla, VexxenUnavailableException: quien llama decide si deja pasar.
    /// </summary>
    Task<VexxenAccountStatus> GetAccountStatusAsync(string vexxenUserId, CancellationToken cancellationToken);

    /// <summary>Renueva la sesión: el refresh token se gasta y llega otro par.</summary>
    Task<VexxenTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Revoca el refresh token. No falla si ya no valía.</summary>
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
}
