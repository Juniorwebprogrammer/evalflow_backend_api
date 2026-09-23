using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.EvaluationCycles;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;

namespace evalflow_backend_api.Tests.Features.EvaluationCycles.GetAllEvaluationCycles;

public class GetAllEvaluationCyclesEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_EvaluationCycles_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/evaluation-cycles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_EvaluationCycles_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/evaluation-cycles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_EvaluationCycles_WithValidJwt_ReturnsOwnCompanyCycles()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "employee@example.com");
        EvaluationCycle cycle;

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // EmpresaID needs the company row id EF just generated above.
            cycle = new EvaluationCycle
            {
                Nombre = "Q1 Review",
                Activo = false,
                FechaInicio = DateTime.UtcNow,
                FechaFin = DateTime.UtcNow.AddMonths(1),
                EmpresaID = company.Id,
            };
            db.EvaluationCycles.Add(cycle);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.GetAsync("/evaluation-cycles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<List<EvaluationCycleSummaryDto>>();
        payload.Should().ContainSingle(c => c.Nombre == "Q1 Review");
    }
}
