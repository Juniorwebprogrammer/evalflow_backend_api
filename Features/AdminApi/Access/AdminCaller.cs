using System.Security.Claims;
using System.Text.Json;
using evalflow_backend_api.Features.AdminApi.Contract;

namespace evalflow_backend_api.Features.AdminApi.Access;

/// <summary>A permission of the caller in EvalFlow. Tenant null: all of EvalFlow's data.</summary>
public sealed record AdminGrant(string? Tenant, string Role);

/// <summary>The outcome of the authorization rules: allowed, or the 403 code and why.</summary>
public sealed record AdminAccessDecision(bool Allowed, string? Code = null, string? Detail = null)
{
    public static readonly AdminAccessDecision Allow = new(true);
    public static AdminAccessDecision Deny(string code, string detail) => new(false, code, detail);
}

/// <summary>
/// Who calls the Admin API, read from the Vexxen Administrator access token (contract §5) once the VexxenAdmin scheme
/// has validated it (signature, alg, typ, iss, aud, exp). Email and name are only for the logs, never to authorize.
/// </summary>
public sealed record AdminCaller(string Sub, string? Email, string? Name, string? ClientId, IReadOnlySet<string> Scopes, IReadOnlyList<AdminGrant> Grants)
{
    public const string ItemKey = "AdminApi.Caller";

    /// <summary>
    /// Can run actions (contract §12.1): the scope admin:write and some operator grant. Either alone isn't enough.
    /// </summary>
    public bool CanWrite => Scopes.Contains(AdminContract.WriteScope) && Grants.Any(g => g.Role == AdminContract.OperatorRole);

    /// <summary>
    /// The 403 rules of §5.2 for a valid token: scope admin:read, at least one grant and no tenant-scoped grant
    /// (EvalFlow doesn't filter by company yet, so it can't treat one as full access: unsupported_grant).
    /// </summary>
    public AdminAccessDecision CheckRead()
    {
        if (!Scopes.Contains(AdminContract.ReadScope))
            return AdminAccessDecision.Deny(AdminProblemCodes.Forbidden, "The token can't read admin data (scope admin:read)");
        if (Grants.Count == 0)
            return AdminAccessDecision.Deny(AdminProblemCodes.Forbidden, "No access to this app");
        if (Grants.Any(g => g.Tenant is not null))
            return AdminAccessDecision.Deny(AdminProblemCodes.UnsupportedGrant, "This app doesn't support tenant-scoped access yet");
        return AdminAccessDecision.Allow;
    }

    public AdminAccessDecision CheckWrite()
    {
        var read = CheckRead();
        if (!read.Allowed) return read;
        return CanWrite
            ? AdminAccessDecision.Allow
            : AdminAccessDecision.Deny(AdminProblemCodes.Forbidden, "Actions need the scope admin:write and the operator role");
    }

    public static AdminCaller FromPrincipal(ClaimsPrincipal principal)
    {
        string? Claim(string type) => principal.FindFirst(type)?.Value;

        var scopes = principal.FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);

        return new AdminCaller(
            Claim("sub") ?? "",
            Claim("email"),
            Claim("name"),
            Claim("client_id"),
            scopes,
            ParseGrants(principal.FindAll("grants").Select(c => c.Value)));
    }

    /// <summary>
    /// The grants claim is a JSON array of { tenant, role }. Depending on the token handler it arrives as one claim
    /// with the whole array or as one claim per object, so both are accepted. Malformed entries and unknown roles are
    /// dropped (no grant means no access, never more access).
    /// </summary>
    public static IReadOnlyList<AdminGrant> ParseGrants(IEnumerable<string> claimValues)
    {
        var grants = new List<AdminGrant>();
        foreach (var raw in claimValues)
        {
            var value = raw.Trim();
            if (value.Length == 0) continue;
            try
            {
                using var document = JsonDocument.Parse(value);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in root.EnumerateArray()) AddGrant(element, grants);
                }
                else
                {
                    AddGrant(root, grants);
                }
            }
            catch (JsonException)
            {
                // Not JSON: not a grant.
            }
        }
        return grants;
    }

    private static void AddGrant(JsonElement element, List<AdminGrant> grants)
    {
        if (element.ValueKind != JsonValueKind.Object) return;
        if (!element.TryGetProperty("role", out var role) || role.ValueKind != JsonValueKind.String) return;
        var roleName = role.GetString()!;
        if (!AdminContract.Roles.Contains(roleName)) return;

        string? tenant;
        if (!element.TryGetProperty("tenant", out var tenantElement) || tenantElement.ValueKind == JsonValueKind.Null) tenant = null;
        else if (tenantElement.ValueKind == JsonValueKind.String) tenant = tenantElement.GetString();
        // Any other tenant shape is still a tenant: never read it as "all the data".
        else tenant = tenantElement.GetRawText();

        grants.Add(new AdminGrant(tenant, roleName));
    }

    public static AdminCaller From(HttpContext context) =>
        context.Items[ItemKey] as AdminCaller ?? throw new InvalidOperationException("The Admin API caller wasn't resolved (AdminApiAccessFilter).");
}
