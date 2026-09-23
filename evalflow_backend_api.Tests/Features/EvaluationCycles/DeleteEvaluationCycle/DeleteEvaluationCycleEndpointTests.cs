using System.Net;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.EvaluationCycles.DeleteEvaluationCycle;

public class DeleteEvaluationCycleEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Delete_EvaluationCycle_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.DeleteAsync("/evaluation-cycles/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_EvaluationCycle_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.DeleteAsync("/evaluation-cycles/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_EvaluationCycle_WithValidOwnerJwt_DeletesInactiveCycle()
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
                Nombre = "Inactive cycle",
                Activo = false,
                FechaInicio = DateTime.UtcNow,
                FechaFin = DateTime.UtcNow.AddMonths(1),
                EmpresaID = company.Id,
            };
            db.EvaluationCycles.Add(cycle);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.DeleteAsync($"/evaluation-cycles/{cycle.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.EvaluationCycles.FindAsync(cycle.Id)).Should().BeNull();
    }
}
