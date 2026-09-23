using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.Companies.UpdateCompaniesInformation;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Companies.UpdateCompaniesInformation;

public class UpdateCompaniesEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_CompanyUpdate_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsJsonAsync("/Company/update", new UpdateCompaniesInformationRecord("New Name", "logo.png", "#fff", "B12345678", "Address", "Tech"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_CompanyUpdate_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync("/Company/update", new UpdateCompaniesInformationRecord("New Name", "logo.png", "#fff", "B12345678", "Address", "Tech"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_CompanyUpdate_WithEmployeeRole_ReturnsForbidden()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, rol: AppRoles.Employee);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PutAsJsonAsync("/Company/update", new UpdateCompaniesInformationRecord("New Name", "logo.png", "#fff", "B12345678", "Address", "Tech"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_CompanyUpdate_WithRrhhRole_UpdatesCompany()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1", nombre: "Old Name");
        var user = TestDataFactory.CreateUser(company, rol: AppRoles.Rrhh);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PutAsJsonAsync("/Company/update", new UpdateCompaniesInformationRecord("New Name", "logo.png", "#123456", "B87654321", "New Address", "Tech"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Companies.SingleAsync()).Nombre.Should().Be("New Name");
    }
}
