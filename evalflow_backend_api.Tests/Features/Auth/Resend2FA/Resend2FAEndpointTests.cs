using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Auth.Resend2FA;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Auth.Resend2FA;

public class Resend2FAEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_Resend2FA_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync("/auth/resend-2fa", new Resend2FARecord("user@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Resend2FA_WithUnknownEmail_ReturnsOkAndSendsNoEmail()
    {
        var response = await Client.PostAsJsonAsync("/auth/resend-2fa", new Resend2FARecord("nobody@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Factory.FakeEmailService.SentEmails.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_Resend2FA_WithTwoFactorEnabledUser_SendsNewCode()
    {
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", twoFactorEnabled: true);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/auth/resend-2fa", new Resend2FARecord("user@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Factory.FakeEmailService.SentEmails.Should().ContainSingle(e => e.To == "user@example.com");
    }
}
