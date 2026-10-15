using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Infrastructure.Vexxen;

public class VexxenClient(HttpClient httpClient, IOptions<VexxenSettings> options, ILogger<VexxenClient> logger) : IVexxenClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly VexxenSettings _settings = options.Value;

    public async Task<VexxenUser> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/auth/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await SendAsync(request, cancellationToken);

        // 401: token caducado o no válido. 404: la cuenta ya no existe (tampoco sirve su token).
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
            throw new VexxenUnauthorizedException("The Vexxen session isn't valid");
        return await ReadAsync<VexxenUser>(response, cancellationToken);
    }

    public async Task<VexxenAccess> GetAccessAsync(string vexxenUserId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/access/{Uri.EscapeDataString(vexxenUserId)}");
        using var response = await SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound && await CodeOf(response, cancellationToken) == "USER_NOT_FOUND")
            throw new VexxenUserNotFoundException(vexxenUserId);
        return await ReadAsync<VexxenAccess>(response, cancellationToken);
    }

    public async Task<VexxenTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/auth/refreshToken");
        request.Content = JsonContent.Create(new { refreshToken }, options: Json);
        using var response = await SendAsync(request, cancellationToken);

        // 401: caducado, revocado (cambio de contraseña, logout) o de otro cliente. 400: ni siquiera es un token.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            throw new VexxenUnauthorizedException("The Vexxen session has expired");
        return await ReadAsync<VexxenTokens>(response, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/auth/logout");
        request.Content = JsonContent.Create(new { refreshToken }, options: Json);
        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            logger.LogWarning("Vexxen logout answered {Status}", (int)response.StatusCode);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Client-Id", _settings.ClientId);
        request.Headers.Add("X-Api-Key", _settings.ApiKey);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Vexxen didn't answer {Method} {Path}", request.Method, request.RequestUri);
            throw new VexxenUnavailableException("Vexxen isn't available", ex);
        }
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            // 401 con la API key, 403 CLIENT_WITHOUT_PRODUCT, 5xx…: un problema de Vexxen o de configuración.
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Vexxen answered {Status} to {Path}: {Body}", (int)response.StatusCode, response.RequestMessage?.RequestUri, Truncate(body));
            throw new VexxenUnavailableException($"Vexxen answered {(int)response.StatusCode}");
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
                   ?? throw new VexxenUnavailableException("Vexxen sent an empty response");
        }
        catch (JsonException ex)
        {
            throw new VexxenUnavailableException("Vexxen sent an unexpected response", ex);
        }
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);
            return body.TryGetProperty("code", out var code) ? code.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value) => value.Length > 300 ? value[..300] : value;
}
