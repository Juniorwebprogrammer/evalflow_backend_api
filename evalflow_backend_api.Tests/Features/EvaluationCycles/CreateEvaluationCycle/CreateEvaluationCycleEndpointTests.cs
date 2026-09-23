using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.EvaluationCycles.CreateEvaluationCycle;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.EvaluationCycles.CreateEvaluationCycle;

public class CreateEvaluationCycleEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_EvaluationCycles_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync(
            "/evaluation-cycles",
            new CreateEvaluationCycleBody("Q1 Review", null, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_EvaluationCycles_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync(
            "/evaluation-cycles",
            new CreateEvaluationCycleBody("Q1 Review", null, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_EvaluationCycles_WithWrongRole_ReturnsForbidden()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "employee@example.com", rol: AppRoles.Employee);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PostAsJsonAsync(
            "/evaluation-cycles",
            new CreateEvaluationCycleBody("Q1 Review", null, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_EvaluationCycles_WithValidOwnerJwt_CreatesCycle()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PostAsJsonAsync(
            "/evaluation-cycles",
            new CreateEvaluationCycleBody("Q1 Review", "Quarterly review", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.EvaluationCycles.SingleAsync()).Nombre.Should().Be("Q1 Review");
    }
}
