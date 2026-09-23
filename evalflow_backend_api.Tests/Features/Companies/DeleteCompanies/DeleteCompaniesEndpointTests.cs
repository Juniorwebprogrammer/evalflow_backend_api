using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.Companies.DeleteCompanies;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Companies.DeleteCompanies;

public class DeleteCompaniesEndpointTests : IntegrationTestBase
{
    private static HttpRequestMessage BuildDeleteRequest(DeleteCompaniesRecord record) =>
        new(HttpMethod.Delete, "/Company/delete") { Content = JsonContent.Create(record) };

    [Fact]
    public async Task Delete_Company_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.SendAsync(BuildDeleteRequest(new DeleteCompaniesRecord("whatever")));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Company_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.SendAsync(BuildDeleteRequest(new DeleteCompaniesRecord("whatever")));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Company_WithEmployeeRole_ReturnsForbidden()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, rol: AppRoles.Employee, passwordHash: BCrypt.Net.BCrypt.HashPassword("correct-password"));

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.SendAsync(BuildDeleteRequest(new DeleteCompaniesRecord("correct-password")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_Company_WithOwnerRoleAndCorrectPassword_DeletesCompany()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, rol: AppRoles.Owner, passwordHash: BCrypt.Net.BCrypt.HashPassword("correct-password"));

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.SendAsync(BuildDeleteRequest(new DeleteCompaniesRecord("correct-password")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Companies.AnyAsync(c => c.IdentificationId == "tenant-1")).Should().BeFalse();
    }
}
