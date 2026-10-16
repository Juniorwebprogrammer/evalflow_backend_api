using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using evalflow_backend_api.Tests.Common;
using Microsoft.IdentityModel.Tokens;

namespace evalflow_backend_api.Tests.Features.AdminApi;

/// <summary>
/// Who gets into /admin/v1 (contract §5.2): only valid Vexxen Administrator tokens (401 otherwise), with the read scope,
/// some grant and no tenant (403 otherwise). And the two schemes stay apart: EvalFlow's JWT doesn't open the Admin API
/// and an admin token doesn't open the rest of the API.
/// </summary>
public class AdminApiAuthTests : IntegrationTestBase
{
    private HttpClient AdminClient(string? token)
    {
        // The desktop app sends no X-Api-Key: the Admin API doesn't ask for it.
        var client = CreateClient(includeApiKey: false);
        if (token is not null) client.DefaultRequestHeaders.Authorization = VexxenAdminTokens.Bearer(token);
        return client;
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        problem.GetProperty("type").GetString().Should().Be("about:blank");
        problem.GetProperty("status").GetInt32().Should().Be((int)response.StatusCode);
        problem.GetProperty("requestId").GetString().Should().NotBeNullOrEmpty();
        return problem;
    }

    [Fact]
    public async Task ValidViewerToken_ReadsTheManifest()
    {
        var response = await AdminClient(VexxenAdminTokens.Viewer()).GetAsync("/admin/v1/manifest");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.GetValues("X-Request-Id").Single().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task WithoutToken_401ProblemWithWwwAuthenticate()
    {
        var response = await AdminClient(null).GetAsync("/admin/v1/health");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().Should().Contain("error=\"invalid_token\"");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await ProblemAsync(response)).GetProperty("code").GetString().Should().Be("unauthorized");
    }

    public static TheoryData<string, Func<string>> InvalidTokens => new()
    {
        { "wrong audience", () => VexxenAdminTokens.Create(audience: "planify") },
        { "no audience", () => VexxenAdminTokens.Create(audience: null) },
        { "wrong issuer", () => VexxenAdminTokens.Create(issuer: "https://evil.test") },
        { "typ JWT instead of at+jwt", () => VexxenAdminTokens.Create(type: "JWT") },
        { "expired beyond the 60 s tolerance", () => VexxenAdminTokens.Create(expires: DateTime.UtcNow.AddMinutes(-5)) },
        { "signed by another key", () => VexxenAdminTokens.Create(key: new ECDsaSecurityKey(ECDsa.Create(ECCurve.NamedCurves.nistP256)) { KeyId = VexxenAdminTokens.Kid }) },
        { "HS256 (algorithm confusion)", () => VexxenAdminTokens.Create(key: VexxenAdminTokens.SymmetricKey(), algorithm: SecurityAlgorithms.HmacSha256) },
        { "garbage", () => "not-a-jwt" },
    };

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public async Task InvalidTokens_Are401(string why, Func<string> token)
    {
        var response = await AdminClient(token()).GetAsync("/admin/v1/manifest");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, why);
        (await ProblemAsync(response)).GetProperty("code").GetString().Should().Be("unauthorized");
    }

    [Fact]
    public async Task ExpiredWithinTheTolerance_StillWorks()
    {
        var token = VexxenAdminTokens.Create(expires: DateTime.UtcNow.AddSeconds(-20));

        var response = await AdminClient(token).GetAsync("/admin/v1/manifest");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithoutTheReadScope_403Forbidden()
    {
        var response = await AdminClient(VexxenAdminTokens.Create(scope: "openid")).GetAsync("/admin/v1/manifest");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ProblemAsync(response)).GetProperty("code").GetString().Should().Be("forbidden");
    }

    [Fact]
    public async Task WithoutGrants_403Forbidden()
    {
        var response = await AdminClient(VexxenAdminTokens.Create(grants: [])).GetAsync("/admin/v1/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ProblemAsync(response)).GetProperty("code").GetString().Should().Be("forbidden");
    }

    [Fact]
    public async Task ATenantScopedGrant_403UnsupportedGrant()
    {
        var token = VexxenAdminTokens.Create(grants: [VexxenAdminTokens.Grant("viewer", tenant: "acme")]);

        var response = await AdminClient(token).GetAsync("/admin/v1/companies");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ProblemAsync(response)).GetProperty("code").GetString().Should().Be("unsupported_grant");
    }

    [Fact]
    public async Task EvalFlowJwt_DoesntOpenTheAdminApi()
    {
        var company = TestDataFactory.CreateCompany();
        var admin = TestDataFactory.CreateUser(company, rol: "Administrator");
        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }

        var response = await AdminClient(GenerateJwt(admin, company)).GetAsync("/admin/v1/manifest");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminToken_DoesntOpenTheRestOfTheApi()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", VexxenAdminTokens.Operator());

        var response = await client.GetAsync("/dashboard/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestId_IsEchoed()
    {
        var client = AdminClient(VexxenAdminTokens.Viewer());
        client.DefaultRequestHeaders.Add("X-Request-Id", "req-123");

        var ok = await client.GetAsync("/admin/v1/health");
        var notFound = await client.GetAsync("/admin/v1/nope");

        ok.Headers.GetValues("X-Request-Id").Single().Should().Be("req-123");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProblemAsync(notFound)).GetProperty("requestId").GetString().Should().Be("req-123");
    }

    [Fact]
    public async Task OtherMethods_405MethodNotAllowed()
    {
        var response = await AdminClient(VexxenAdminTokens.Operator()).DeleteAsync("/admin/v1/companies/1");

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        (await ProblemAsync(response)).GetProperty("code").GetString().Should().Be("method_not_allowed");
    }
}
