using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Auth.ForgotPassword;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Auth.ForgotPassword;

public class ForgotPasswordEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_ForgotPassword_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync("/auth/forgot-password", new ForgotPasswordBody("user@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ForgotPassword_WithUnknownEmail_ReturnsOkAndSendsNoEmail()
    {
        var response = await Client.PostAsJsonAsync("/auth/forgot-password", new ForgotPasswordBody("nobody@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Factory.FakeEmailService.SentEmails.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_ForgotPassword_WithActiveUser_SendsRecoveryEmail()
    {
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "active@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/auth/forgot-password", new ForgotPasswordBody("active@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Factory.FakeEmailService.SentEmails.Should().ContainSingle(e => e.To == "active@example.com");
    }
}
