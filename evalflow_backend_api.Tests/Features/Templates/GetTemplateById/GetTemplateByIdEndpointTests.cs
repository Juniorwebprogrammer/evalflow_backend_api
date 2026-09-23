using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Templates.TemplatesDto;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;

namespace evalflow_backend_api.Tests.Features.Templates.GetTemplateById;

public class GetTemplateByIdEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_TemplateById_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/templates/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_TemplateById_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/templates/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_TemplateById_WithNonExistentId_ReturnsNotFound()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com", rol: AppRoles.Employee);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(employee);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(employee, company);

        var response = await client.GetAsync("/templates/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_TemplateById_WithValidJwt_ReturnsTemplateDetails()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com", rol: AppRoles.Employee);
        Template template;

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(employee);
            await db.SaveChangesAsync();

            // EmpresaID needs the company row id EF just generated above.
            template = new Template
            {
                Titulo = "Evaluación anual",
                FechaInicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FechaFin = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EmpresaID = company.Id,
            };
            db.Templates.Add(template);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(employee, company);

        var response = await client.GetAsync($"/templates/{template.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<TemplateDetailsDto>();
        payload!.Titulo.Should().Be("Evaluación anual");
    }
}
