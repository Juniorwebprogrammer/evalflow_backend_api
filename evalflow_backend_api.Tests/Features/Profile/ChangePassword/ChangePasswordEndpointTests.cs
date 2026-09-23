using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Profile.ChangePassword;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Profile.ChangePassword;

public class ChangePasswordEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_ChangePassword_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsJsonAsync("/Profile/change-password", new ChangePasswordRecord("old-password", "new-password"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_ChangePassword_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync("/Profile/change-password", new ChangePasswordRecord("old-password", "new-password"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_ChangePassword_WithValidJwtAndCorrectPassword_UpdatesPassword()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "owner@example.com", passwordHash: BCrypt.Net.BCrypt.HashPassword("correct-password"));

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PutAsJsonAsync("/Profile/change-password", new ChangePasswordRecord("correct-password", "new-password"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        var updated = await verifyDb.Users.SingleAsync(u => u.Id == user.Id);
        BCrypt.Net.BCrypt.Verify("new-password", updated.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Put_ChangePassword_WithWrongCurrentPassword_ReturnsBadRequest()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "owner2@example.com", passwordHash: BCrypt.Net.BCrypt.HashPassword("correct-password"));

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PutAsJsonAsync("/Profile/change-password", new ChangePasswordRecord("wrong-password", "new-password"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
