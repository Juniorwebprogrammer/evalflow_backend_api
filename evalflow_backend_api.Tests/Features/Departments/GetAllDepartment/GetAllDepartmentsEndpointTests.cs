using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Departments.GetAllDepartment;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Departments.GetAllDepartment;

public class GetAllDepartmentsEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_Departments_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/departments");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Departments_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/departments");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Departments_WithValidJwt_ReturnsOnlyOwnDepartments()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        var user = TestDataFactory.CreateUser(company);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.AddRange(company, otherCompany);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // EmpresaID needs the company row ids EF just generated above.
            Department ownDepartment = new() { Nombre = "Engineering", EmpresaID = company.Id };
            Department otherDepartment = new() { Nombre = "Other Co Dept", EmpresaID = otherCompany.Id };
            db.Departments.AddRange(ownDepartment, otherDepartment);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.GetAsync("/departments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<List<DepartmentSummaryDto>>();
        payload!.Should().ContainSingle(d => d.Nombre == "Engineering");
    }
}
