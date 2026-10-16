using System.Security.Claims;
using evalflow_backend_api.Features.AdminApi.Access;

namespace evalflow_backend_api.Tests.Features.AdminApi;

/// <summary>The authorization rules after the token is valid (contract §5.2 and §12.1).</summary>
public class AdminCallerTests
{
    private static AdminCaller Caller(string scope, params string[] grantClaims)
    {
        var claims = new List<Claim> { new("sub", "vx-admin"), new("email", "admin@vexxen.test"), new("scope", scope) };
        claims.AddRange(grantClaims.Select(g => new Claim("grants", g)));
        return AdminCaller.FromPrincipal(new ClaimsPrincipal(new ClaimsIdentity(claims, "VexxenAdmin")));
    }

    private const string Viewer = """{"tenant":null,"role":"viewer"}""";
    private const string Operator = """{"tenant":null,"role":"operator"}""";

    [Fact]
    public void Grants_AsOneClaimWithTheWholeArray()
    {
        var caller = Caller("admin:read", $"[{Viewer},{Operator}]");

        caller.Grants.Should().BeEquivalentTo([new AdminGrant(null, "viewer"), new AdminGrant(null, "operator")]);
    }

    [Fact]
    public void Grants_AsOneClaimPerObject()
    {
        var caller = Caller("admin:read", Viewer, Operator);

        caller.Grants.Should().HaveCount(2);
    }

    [Fact]
    public void Grants_MalformedOrUnknownRoles_AreDropped()
    {
        var caller = Caller("admin:read", "not json", """{"tenant":null,"role":"owner"}""", """{"tenant":null}""", "[]");

        caller.Grants.Should().BeEmpty();
        caller.CheckRead().Code.Should().Be("forbidden");
    }

    [Fact]
    public void Read_NeedsTheReadScope()
    {
        var decision = Caller("openid", Viewer).CheckRead();

        decision.Allowed.Should().BeFalse();
        decision.Code.Should().Be("forbidden");
    }

    [Fact]
    public void Read_NeedsSomeGrant()
    {
        Caller("admin:read").CheckRead().Code.Should().Be("forbidden");
    }

    [Fact]
    public void ATenantScopedGrant_IsUnsupported_NeverFullAccess()
    {
        var decision = Caller("admin:read", Viewer, """{"tenant":"acme","role":"viewer"}""").CheckRead();

        decision.Allowed.Should().BeFalse();
        decision.Code.Should().Be("unsupported_grant");
    }

    [Fact]
    public void Viewer_CanRead_ButNotWrite()
    {
        var caller = Caller("admin:read", Viewer);

        caller.CheckRead().Allowed.Should().BeTrue();
        caller.CanWrite.Should().BeFalse();
        caller.CheckWrite().Code.Should().Be("forbidden");
    }

    [Fact]
    public void Operator_WithTheWriteScope_CanWrite()
    {
        var caller = Caller("admin:read admin:write", Operator);

        caller.CanWrite.Should().BeTrue();
        caller.CheckWrite().Allowed.Should().BeTrue();
    }

    [Fact]
    public void Operator_WithoutTheWriteScope_CantWrite()
    {
        Caller("admin:read", Operator).CanWrite.Should().BeFalse();
    }

    [Fact]
    public void WriteScope_WithoutTheOperatorRole_CantWrite()
    {
        Caller("admin:read admin:write", Viewer).CanWrite.Should().BeFalse();
    }

    [Fact]
    public void Write_StillNeedsTheReadRules()
    {
        Caller("admin:write", Operator).CheckWrite().Code.Should().Be("forbidden");
    }
}
