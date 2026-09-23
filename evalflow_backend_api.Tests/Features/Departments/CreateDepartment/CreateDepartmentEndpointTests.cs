using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Departments.CreateDepartment;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Departments.CreateDepartment;

public class CreateDepartmentEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_Departments_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/departments", new CreateDepartmentRecord("Engineering", null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Departments_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync("/departments", new CreateDepartmentRecord("Engineering", null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Departments_WithValidJwt_CreatesDepartment()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "owner@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PostAsJsonAsync("/departments", new CreateDepartmentRecord("Engineering", "Builds the product"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Departments.SingleAsync()).Nombre.Should().Be("Engineering");
    }
}
