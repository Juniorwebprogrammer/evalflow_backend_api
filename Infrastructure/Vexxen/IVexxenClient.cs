namespace evalflow_backend_api.Infrastructure.Vexxen;

/// <summary>
/// La API de Vexxen, como la usa @vexxen/auth: cabeceras X-Client-Id / X-Api-Key y, para el usuario, su access token.
/// Lanza VexxenUnauthorizedException, VexxenUserNotFoundException o VexxenUnavailableException.
/// </summary>
public interface IVexxenClient
{
    Task<VexxenUser> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken);

    /// <summary>El acceso del usuario al producto de EvalFlow (su plan).</summary>
    Task<VexxenAccess> GetAccessAsync(string vexxenUserId, CancellationToken cancellationToken);

    /// <summary>Renueva la sesión: el refresh token se gasta y llega otro par.</summary>
    Task<VexxenTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Revoca el refresh token. No falla si ya no valía.</summary>
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
}
