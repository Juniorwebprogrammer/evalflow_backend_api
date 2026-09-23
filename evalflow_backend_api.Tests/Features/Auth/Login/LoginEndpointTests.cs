using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Auth.Login;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;

namespace evalflow_backend_api.Tests.Features.Auth.Login;

public class LoginEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_Login_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync("/Auth/login", new LoginRecord("user@example.com", "password", "tenant-1"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Login_WithUnknownUser_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/Auth/login", new LoginRecord("nobody@example.com", "password", "tenant-1"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Login_WithValidCredentials_ReturnsOkWithJwt()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", passwordHash: BCrypt.Net.BCrypt.HashPassword("correct-password"));

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/Auth/login", new LoginRecord("user@example.com", "correct-password", "tenant-1"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        payload!.Jwt.Should().NotBeNullOrEmpty();
        payload.TwoFactorEnabled.Should().BeFalse();
    }
}
