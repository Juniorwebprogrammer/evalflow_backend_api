using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Departments.GetDepartment;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Departments.GetDepartment;

public class GetDepartmentEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_Department_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/departments/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Department_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/departments/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Department_BelongingToAnotherCompany_ReturnsNotFound()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        var user = TestDataFactory.CreateUser(company);
        Department otherDepartment;

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.AddRange(company, otherCompany);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // EmpresaID needs the company row id EF just generated above.
            otherDepartment = new Department { Nombre = "Other Co Dept", EmpresaID = otherCompany.Id };
            db.Departments.Add(otherDepartment);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.GetAsync($"/departments/{otherDepartment.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Department_WithValidJwt_ReturnsDepartmentDetails()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company);
        Department department;

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // EmpresaID needs the company row id EF just generated above.
            department = new Department { Nombre = "Engineering", EmpresaID = company.Id };
            db.Departments.Add(department);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.GetAsync($"/departments/{department.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<DepartmentDetailsDto>();
        payload!.Nombre.Should().Be("Engineering");
    }
}
