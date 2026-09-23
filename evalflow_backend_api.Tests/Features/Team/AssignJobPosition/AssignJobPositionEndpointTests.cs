using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Team.AssignJobPosition;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Team.AssignJobPosition;

public class AssignJobPositionEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_AssignJobPosition_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsJsonAsync("/team/1/job-position", new AssignJobPositionBody(null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_AssignJobPosition_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync("/team/1/job-position", new AssignJobPositionBody(null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_AssignJobPosition_WithEmployeeRole_ReturnsForbidden()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var caller = TestDataFactory.CreateUser(company, email: "employee-caller@example.com", rol: AppRoles.Employee);
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.AddRange(caller, employee);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(caller, company);

        var response = await client.PutAsJsonAsync($"/team/{employee.Id}/job-position", new AssignJobPositionBody(null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_AssignJobPosition_WithOwnerRole_AssignsPositionAndReturnsOk()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var owner = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
        var position = new JobPosition { Nombre = "Analista", EmpresaID = company.Id, Empresa = company };

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.AddRange(owner, employee);
            db.JobPositions.Add(position);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(owner, company);

        var response = await client.PutAsJsonAsync($"/team/{employee.Id}/job-position", new AssignJobPositionBody(position.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Users.SingleAsync(u => u.Id == employee.Id)).CargoId.Should().Be(position.Id);
    }
}
