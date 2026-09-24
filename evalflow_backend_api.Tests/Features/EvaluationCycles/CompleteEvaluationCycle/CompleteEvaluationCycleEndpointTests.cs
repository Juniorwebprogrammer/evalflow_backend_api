using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using evalflow_backend_api.Tests.Common;
using evalflow_backend_api.Tests.Features.EvaluationResults;
using Microsoft.Extensions.DependencyInjection;

namespace evalflow_backend_api.Tests.Features.EvaluationCycles.CompleteEvaluationCycle;

public class CompleteEvaluationCycleEndpointTests : IntegrationTestBase
{
    private async Task<ResultsScenario> SeedAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        await using var db = Factory.CreateDbContext();
        return await ResultsTestData.SeedAsync(db, encryption.Encrypt);
    }

    [Fact]
    public async Task Post_Complete_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PostAsync("/evaluation-cycles/1/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Complete_WithEmployeeRole_ReturnsForbidden()
    {
        var s = await SeedAsync();
        var client = CreateAuthenticatedClient(s.Employee, s.Company);

        var response = await client.PostAsync($"/evaluation-cycles/{s.Cycle.Id}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Accept_Complete_AndDownloadReport_FullFlow()
    {
        var s = await SeedAsync();
        var rrhh = CreateAuthenticatedClient(s.Rrhh, s.Company);
        var employee = CreateAuthenticatedClient(s.Employee, s.Company);

        var blocked = await rrhh.PostAsync($"/evaluation-cycles/{s.Cycle.Id}/complete", null);
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var comparisons = await rrhh.GetFromJsonAsync<JsonElement>($"/evaluation-cycles/{s.Cycle.Id}/comparisons");
        comparisons.GetProperty("pendingImbalances").GetInt32().Should().Be(1);

        var accept = await rrhh.PutAsJsonAsync($"/evaluation-cycles/{s.Cycle.Id}/discrepancies/acceptances",
            new { evaluatedUserId = s.Employee.Id, templateId = s.Template.Id, questionIds = new[] { s.Imbalanced.Id }, source = "Autoevaluacion" });
        accept.StatusCode.Should().Be(HttpStatusCode.OK);

        comparisons = await rrhh.GetFromJsonAsync<JsonElement>($"/evaluation-cycles/{s.Cycle.Id}/comparisons");
        comparisons.GetProperty("pendingImbalances").GetInt32().Should().Be(0);
        var question = comparisons.GetProperty("comparisons")[0].GetProperty("questions").EnumerateArray()
            .Single(q => q.GetProperty("questionId").GetInt32() == s.Imbalanced.Id);
        question.GetProperty("acceptedSource").GetString().Should().Be("Autoevaluacion");

        var complete = await rrhh.PostAsync($"/evaluation-cycles/{s.Cycle.Id}/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);

        comparisons = await rrhh.GetFromJsonAsync<JsonElement>($"/evaluation-cycles/{s.Cycle.Id}/comparisons");
        comparisons.GetProperty("isCompleted").GetBoolean().Should().BeTrue();

        var mine = await employee.GetFromJsonAsync<JsonElement>("/evaluation-results/mine");
        var resultId = mine.EnumerateArray().Should().ContainSingle().Subject.GetProperty("id").GetInt32();

        var cycleResults = await rrhh.GetFromJsonAsync<JsonElement>($"/evaluation-cycles/{s.Cycle.Id}/results");
        cycleResults.GetArrayLength().Should().Be(1);

        var pdf = await employee.GetAsync($"/evaluation-results/{resultId}/pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await pdf.Content.ReadAsByteArrayAsync()).Take(4).Should().Equal("%PDF"u8.ToArray());

        var managerClient = CreateAuthenticatedClient(s.Manager, s.Company);
        (await managerClient.GetAsync($"/evaluation-results/{resultId}/pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await rrhh.PostAsync($"/evaluation-cycles/{s.Cycle.Id}/complete", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
