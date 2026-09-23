using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.EvaluationCycles.UpdateEvaluationCycle;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.EvaluationCycles.UpdateEvaluationCycle;

public class UpdateEvaluationCycleEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_EvaluationCycle_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsJsonAsync(
            "/evaluation-cycles/1",
            new UpdateEvaluationCycleBody("Q1 Review", null, true, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_EvaluationCycle_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync(
            "/evaluation-cycles/1",
            new UpdateEvaluationCycleBody("Q1 Review", null, true, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_EvaluationCycle_WithValidOwnerJwt_UpdatesCycle()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);
        EvaluationCycle cycle;

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // EmpresaID needs the company row id EF just generated above.
            cycle = new EvaluationCycle
            {
                Nombre = "Old name",
                Activo = false,
                FechaInicio = DateTime.UtcNow,
                FechaFin = DateTime.UtcNow.AddMonths(1),
                EmpresaID = company.Id,
            };
            db.EvaluationCycles.Add(cycle);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PutAsJsonAsync(
            $"/evaluation-cycles/{cycle.Id}",
            new UpdateEvaluationCycleBody("New name", "New description", true, DateTime.UtcNow, DateTime.UtcNow.AddMonths(2)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        var updated = await verifyDb.EvaluationCycles.FindAsync(cycle.Id);
        updated!.Nombre.Should().Be("New name");
        updated.Activo.Should().BeTrue();
    }
}
