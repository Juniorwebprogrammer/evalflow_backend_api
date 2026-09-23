using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Auth.Login;
using evalflow_backend_api.Features.Auth.Verify2FA;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Auth.Verify2FA;

public class Verify2FAEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_Verify2FA_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync("/auth/verify-2fa", new Verify2FARecord("user@example.com", "123456"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Verify2FA_WithUnknownEmail_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync("/auth/verify-2fa", new Verify2FARecord("nobody@example.com", "123456"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Verify2FA_WithValidCode_ReturnsOkWithJwt()
    {
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        user.TwoFactorCode = "ABC123";
        user.TwoFactorCodeExpiration = DateTime.UtcNow.AddMinutes(5);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/auth/verify-2fa", new Verify2FARecord("user@example.com", "ABC123"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        payload!.Jwt.Should().NotBeNullOrEmpty();
    }
}
