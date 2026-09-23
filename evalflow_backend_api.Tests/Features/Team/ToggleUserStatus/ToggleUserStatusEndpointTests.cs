using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.Team.ToggleUserStatus;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Team.ToggleUserStatus;

public class ToggleUserStatusEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_ToggleUserStatus_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsJsonAsync("/team/1/status", new ToggleUserStatusBody(false));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_ToggleUserStatus_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync("/team/1/status", new ToggleUserStatusBody(false));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_ToggleUserStatus_WithEmployeeRole_ReturnsForbidden()
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

        var response = await client.PutAsJsonAsync($"/team/{employee.Id}/status", new ToggleUserStatusBody(false));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_ToggleUserStatus_WithOwnerRole_UpdatesStatusAndReturnsOk()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var owner = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com", activo: true);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.AddRange(owner, employee);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(owner, company);

        var response = await client.PutAsJsonAsync($"/team/{employee.Id}/status", new ToggleUserStatusBody(false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Users.SingleAsync(u => u.Id == employee.Id)).Activo.Should().BeFalse();
    }
}
