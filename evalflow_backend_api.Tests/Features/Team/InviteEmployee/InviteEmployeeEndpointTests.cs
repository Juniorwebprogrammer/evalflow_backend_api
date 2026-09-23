using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.Team.InviteEmployee;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Team.InviteEmployee;

public class InviteEmployeeEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_InviteEmployee_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync("/Team/invite", new InviteEmployeeRecord("Ana", "Perez", "ana@example.com", AppRoles.Employee));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_InviteEmployee_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/Team/invite", new InviteEmployeeRecord("Ana", "Perez", "ana@example.com", AppRoles.Employee));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_InviteEmployee_WithEmployeeRole_ReturnsForbidden()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var caller = TestDataFactory.CreateUser(company, email: "employee-caller@example.com", rol: AppRoles.Employee);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(caller);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(caller, company);

        var response = await client.PostAsJsonAsync("/Team/invite", new InviteEmployeeRecord("Ana", "Perez", "ana@example.com", AppRoles.Employee));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_InviteEmployee_WithOwnerRole_CreatesEmployeeAndSendsEmail()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var owner = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(owner);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(owner, company);

        var response = await client.PostAsJsonAsync("/Team/invite", new InviteEmployeeRecord("Ana", "Perez", "ana@example.com", AppRoles.Employee));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Users.SingleOrDefaultAsync(u => u.Email == "ana@example.com")).Should().NotBeNull();

        Factory.FakeEmailService.SentEmails.Should().ContainSingle(e => e.To == "ana@example.com");
    }
}
