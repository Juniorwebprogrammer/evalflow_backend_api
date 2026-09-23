using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.Team.AssignSuperior;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Team.AssignSuperior;

public class AssignSuperiorEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_AssignSuperior_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsJsonAsync("/team/1/superior", new AssignSuperiorBody(null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_AssignSuperior_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync("/team/1/superior", new AssignSuperiorBody(null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_AssignSuperior_WithEmployeeRole_ReturnsForbidden()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var caller = TestDataFactory.CreateUser(company, email: "employee-caller@example.com", rol: AppRoles.Employee);
        var superior = TestDataFactory.CreateUser(company, email: "superior@example.com");
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.AddRange(caller, superior, employee);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(caller, company);

        var response = await client.PutAsJsonAsync($"/team/{employee.Id}/superior", new AssignSuperiorBody(superior.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_AssignSuperior_WithOwnerRole_AssignsSuperiorAndReturnsOk()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var caller = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);
        var superior = TestDataFactory.CreateUser(company, email: "superior@example.com");
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.AddRange(caller, superior, employee);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(caller, company);

        var response = await client.PutAsJsonAsync($"/team/{employee.Id}/superior", new AssignSuperiorBody(superior.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Users.SingleAsync(u => u.Id == employee.Id)).SuperiorId.Should().Be(superior.Id);
    }
}
