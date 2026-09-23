using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Profile.DeleteAccount;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Profile.DeleteAccount;

public class DeleteAccountEndpointTests : IntegrationTestBase
{
    private static HttpRequestMessage BuildDeleteRequest(DeleteAccountRecord record) =>
        new(HttpMethod.Delete, "/Profile/delete") { Content = JsonContent.Create(record) };

    [Fact]
    public async Task Delete_Account_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.SendAsync(BuildDeleteRequest(new DeleteAccountRecord("password")));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Account_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.SendAsync(BuildDeleteRequest(new DeleteAccountRecord("password")));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Account_WithValidJwtAndCorrectPassword_DeletesUser()
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

        var response = await client.SendAsync(BuildDeleteRequest(new DeleteAccountRecord("correct-password")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Users.FindAsync(user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Delete_Account_WithWrongPassword_ReturnsBadRequest()
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

        var response = await client.SendAsync(BuildDeleteRequest(new DeleteAccountRecord("wrong-password")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
