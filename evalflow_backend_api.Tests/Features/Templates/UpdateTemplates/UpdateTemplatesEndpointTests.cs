using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Templates.UpdateTemplates;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Templates.UpdateTemplates;

public class UpdateTemplatesEndpointTests : IntegrationTestBase
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Template BuildTemplate(int companyId) => new()
    {
        Titulo = "Evaluación",
        FechaInicio = Start,
        FechaFin = End,
        EmpresaID = companyId,
    };

    [Fact]
    public async Task Put_Templates_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsJsonAsync("/templates/1", new UpdateTemplateBody("Evaluación", null, Start, End, []));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_Templates_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync("/templates/1", new UpdateTemplateBody("Evaluación", null, Start, End, []));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_Templates_WithEmployeeRole_ReturnsForbidden()
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

        var response = await client.PutAsJsonAsync($"/templates/{template.Id}", new UpdateTemplateBody("Nuevo título", null, Start, End, []));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_Templates_WithOwnerRole_UpdatesTemplate()
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

        var response = await client.PutAsJsonAsync($"/templates/{template.Id}", new UpdateTemplateBody("Título actualizado", "Descripción", Start, End, []));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Templates.FindAsync(template.Id))!.Titulo.Should().Be("Título actualizado");
    }
}
