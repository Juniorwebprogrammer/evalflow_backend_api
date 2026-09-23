using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Auth.VerifyEmail;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Auth.VerifyEmail;

public class VerifyEmailEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_VerifyEmail_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync("/auth/verify-email", new VerifyEmailRecord("some-token"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_VerifyEmail_WithUnknownToken_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync("/auth/verify-email", new VerifyEmailRecord("unknown-token"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_VerifyEmail_WithValidToken_MarksEmailVerified()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", emailVerificado: false);
        user.TokenValidacionEmail = "valid-token";
        user.TokenValidacionExpiracion = DateTime.UtcNow.AddHours(1);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/auth/verify-email", new VerifyEmailRecord("valid-token"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        var updated = await verifyDb.Users.SingleAsync(u => u.Email == "user@example.com");
        updated.EmailVerificado.Should().BeTrue();
    }
}
