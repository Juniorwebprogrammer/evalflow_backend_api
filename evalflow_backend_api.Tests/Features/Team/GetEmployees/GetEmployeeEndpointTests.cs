using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Team.GetEmployees;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;

namespace evalflow_backend_api.Tests.Features.Team.GetEmployees;

public class GetEmployeeEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_Employees_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/Team/list");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Employees_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/Team/list");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Employees_WithEmployeeRole_ReturnsForbidden()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var caller = TestDataFactory.CreateUser(company, email: "employee@example.com", rol: AppRoles.Employee);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(caller);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(caller, company);

        var response = await client.GetAsync("/Team/list");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Employees_WithOwnerRole_ReturnsEmployeeList()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var owner = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);
        var position = new JobPosition { Nombre = "Analista", EmpresaID = company.Id, Empresa = company };

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.JobPositions.Add(position);
            db.Users.Add(owner);
            await db.SaveChangesAsync();
            owner.CargoId = position.Id;
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(owner, company);

        var response = await client.GetAsync("/Team/list");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<List<GetEmployeeResponse>>();
        payload.Should().ContainSingle(e => e.Email == "owner@example.com");
    }
}
