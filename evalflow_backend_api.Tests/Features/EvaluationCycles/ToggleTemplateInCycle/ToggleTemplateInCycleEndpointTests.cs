using System.Net;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.EvaluationCycles.ToggleTemplateInCycle;

public class ToggleTemplateInCycleEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_ToggleTemplateInCycle_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsync("/evaluation-cycles/1/templates/1/toggle", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_ToggleTemplateInCycle_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsync("/evaluation-cycles/1/templates/1/toggle", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_ToggleTemplateInCycle_WithValidOwnerJwt_AddsTemplateToCycle()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);
        Template template;
        EvaluationCycle cycle;

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // EmpresaID needs the company row id EF just generated above.
            template = new Template
            {
                Titulo = "Peer Review",
                FechaInicio = DateTime.UtcNow,
                FechaFin = DateTime.UtcNow.AddMonths(1),
                EmpresaID = company.Id,
            };
            cycle = new EvaluationCycle
            {
                Nombre = "Q1 Review",
                FechaInicio = DateTime.UtcNow,
                FechaFin = DateTime.UtcNow.AddMonths(1),
                EmpresaID = company.Id,
            };
            db.Templates.Add(template);
            db.EvaluationCycles.Add(cycle);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PutAsync($"/evaluation-cycles/{cycle.Id}/templates/{template.Id}/toggle", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        var reloaded = await verifyDb.EvaluationCycles.Include(c => c.Templates).SingleAsync(c => c.Id == cycle.Id);
        reloaded.Templates.Should().ContainSingle(t => t.Id == template.Id);
    }
}
