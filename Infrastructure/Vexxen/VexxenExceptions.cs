namespace evalflow_backend_api.Infrastructure.Vexxen;

public abstract class VexxenException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>El access o refresh token no vale (caducado, revocado o de otra app): hay que volver a entrar.</summary>
public class VexxenUnauthorizedException(string message) : VexxenException(message);

/// <summary>
/// La cuenta de Vexxen está suspendida (desde Vexxen Administrator): no puede entrar ni renovar la sesión en ninguna
/// app. Vexxen responde 403 { code: "ACCOUNT_SUSPENDED" }.
/// </summary>
public class VexxenAccountSuspendedException(string? userId = null)
    : VexxenException(userId is null ? "The Vexxen account is suspended" : $"Vexxen user {userId} is suspended");

/// <summary>La cuenta de Vexxen no existe (o se borró).</summary>
public class VexxenUserNotFoundException(string userId) : VexxenException($"Vexxen user {userId} not found");

/// <summary>
/// Vexxen no responde, falla o la configuración no vale (API key, cliente sin producto). Para el usuario es lo mismo:
/// ahora no se puede entrar (503), y las sesiones abiertas siguen con el último plan conocido.
/// </summary>
public class VexxenUnavailableException(string message, Exception? inner = null) : VexxenException(message, inner);
