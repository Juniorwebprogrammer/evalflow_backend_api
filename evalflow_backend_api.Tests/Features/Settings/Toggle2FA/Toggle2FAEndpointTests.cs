using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Settings.Toggle2FA;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Settings.Toggle2FA;

public class Toggle2FAEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_Toggle2FA_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsJsonAsync("/settings/2fa", new Toggle2FARecord(1, true));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_Toggle2FA_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync("/settings/2fa", new Toggle2FARecord(1, true));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_Toggle2FA_WithValidJwt_EnablesTwoFactorForTargetUser()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "owner@example.com", twoFactorEnabled: false);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PutAsJsonAsync("/settings/2fa", new Toggle2FARecord(user.Id, true));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Users.SingleAsync(u => u.Id == user.Id)).TwoFactorEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Put_Toggle2FA_WithUnknownUserId_ReturnsNotFound()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "owner2@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PutAsJsonAsync("/settings/2fa", new Toggle2FARecord(999999, true));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
