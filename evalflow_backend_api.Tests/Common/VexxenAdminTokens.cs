using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace evalflow_backend_api.Tests.Common;

/// <summary>
/// Access tokens of Vexxen Administrator for the tests, like vexxen-home's Es256AdminTokenSigner makes them (ES256,
/// typ at+jwt, kid; claims iss, sub, aud, exp, client_id, scope, email, name, grants). The test host trusts
/// <see cref="SigningKey"/> directly (see CustomWebApplicationFactory), so nothing is fetched over the network.
/// </summary>
public static class VexxenAdminTokens
{
    public const string Issuer = "https://vexxen.test";
    public const string Audience = "evalflow";
    public const string Kid = "test-2026";

    public static readonly ECDsaSecurityKey SigningKey = new(ECDsa.Create(ECCurve.NamedCurves.nistP256)) { KeyId = Kid };

    public static object Grant(string role = "viewer", string? tenant = null) => new Dictionary<string, object?> { ["tenant"] = tenant, ["role"] = role };

    public static string Viewer() => Create();

    public static string Operator() => Create(scope: "admin:read admin:write", grants: [Grant("operator")]);

    public static string Create(
        string scope = "admin:read",
        object[]? grants = null,
        string sub = "vx-admin",
        string? audience = Audience,
        string issuer = Issuer,
        string type = "at+jwt",
        DateTime? expires = null,
        SecurityKey? key = null,
        string algorithm = SecurityAlgorithms.EcdsaSha256)
    {
        // Like Vexxen: valid for 5 minutes from when it was issued.
        var exp = expires is { } at ? new DateTimeOffset(at) : DateTimeOffset.UtcNow.AddMinutes(5);
        var issuedAt = exp.AddMinutes(-5);
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = issuer,
            ["sub"] = sub,
            ["aud"] = audience,
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["nbf"] = issuedAt.ToUnixTimeSeconds(),
            ["exp"] = exp.ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString(),
            ["client_id"] = "vexxen-administrator",
            ["scope"] = scope,
            ["email"] = "admin@vexxen.test",
            ["name"] = "Ada Admin",
            ["grants"] = grants ?? [Grant()],
        };

        var credentials = new SigningCredentials(key ?? SigningKey, algorithm);
        return new JsonWebTokenHandler().CreateToken(JsonSerializer.Serialize(payload), credentials,
            new Dictionary<string, object> { ["typ"] = type });
    }

    /// <summary>A symmetric key, to sign tokens with an algorithm the Admin API must reject (HS256).</summary>
    public static SymmetricSecurityKey SymmetricKey() =>
        new(Encoding.UTF8.GetBytes("test-only-hs256-key-at-least-32-bytes-long!!")) { KeyId = Kid };

    public static AuthenticationHeaderValue Bearer(string token) => new("Bearer", token);
}
