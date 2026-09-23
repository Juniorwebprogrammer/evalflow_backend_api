using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Auth.ResendVerification;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Auth.ResendVerification;

public class ResendVerificationEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_ResendVerification_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync("/auth/resend-verification", new ResendVerificationRecord("user@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ResendVerification_WithUnknownEmail_ReturnsOkAndSendsNoEmail()
    {
        var response = await Client.PostAsJsonAsync("/auth/resend-verification", new ResendVerificationRecord("nobody@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Factory.FakeEmailService.SentEmails.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_ResendVerification_WithUnverifiedUser_SendsNewLink()
    {
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "pending@example.com", emailVerificado: false);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/auth/resend-verification", new ResendVerificationRecord("pending@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Factory.FakeEmailService.SentEmails.Should().ContainSingle(e => e.To == "pending@example.com");
    }
}
