using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Auth.ResetPassword;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Auth.ResetPassword;

public class ResetPasswordEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_ResetPassword_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync("/auth/reset-password", new ResetPasswordBody("some-token", "NewPassword123"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ResetPassword_WithUnknownToken_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync("/auth/reset-password", new ResetPasswordBody("unknown-token", "NewPassword123"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_ResetPassword_WithValidToken_UpdatesPassword()
    {
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", passwordHash: "old-hash");
        user.TokenRecuperacionPassword = "valid-token";
        user.ExpiracionTokenRecuperacion = DateTime.UtcNow.AddMinutes(30);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/auth/reset-password", new ResetPasswordBody("valid-token", "NewPassword123"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        var updated = await verifyDb.Users.SingleAsync(u => u.Email == "user@example.com");
        updated.PasswordHash.Should().NotBe("old-hash");
        updated.TokenRecuperacionPassword.Should().BeNull();
    }
}
