using System.Net;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Templates.DeleteTemplates;

public class DeleteTemplatesEndpointTests : IntegrationTestBase
{
    private static Template BuildTemplate(int companyId) => new()
    {
        Titulo = "Evaluación",
        FechaInicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        FechaFin = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
        EmpresaID = companyId,
    };

    [Fact]
    public async Task Delete_Templates_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.DeleteAsync("/templates/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Templates_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.DeleteAsync("/templates/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Templates_WithEmployeeRole_ReturnsForbidden()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com", rol: AppRoles.Employee);
        var template = BuildTemplate(company.Id);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(employee);
            db.Templates.Add(template);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(employee, company);

        var response = await client.DeleteAsync($"/templates/{template.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_Templates_WithOwnerRole_DeletesTemplate()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var owner = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);
        Template template;

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(owner);
            await db.SaveChangesAsync();

            // EmpresaID needs the company row id EF just generated above.
            template = BuildTemplate(company.Id);
            db.Templates.Add(template);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(owner, company);

        var response = await client.DeleteAsync($"/templates/{template.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Templates.FindAsync(template.Id)).Should().BeNull();
    }
}
