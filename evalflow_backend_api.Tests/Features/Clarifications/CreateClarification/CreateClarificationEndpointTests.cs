using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Clarifications.CreateClarification;

public class CreateClarificationEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_Clarification_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/evaluation-cycles/1/clarifications", new { evaluatedUserId = 1, templateId = 1, mensaje = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Clarification_WithEmployeeRole_ReturnsForbidden()
    {
        ClarificationScenario s;
        await using (var db = Factory.CreateDbContext())
        {
            s = await ClarificationTestData.SeedAsync(db);
        }

        var client = CreateAuthenticatedClient(s.Employee, s.Company);

        var response = await client.PostAsJsonAsync($"/evaluation-cycles/{s.Cycle.Id}/clarifications",
            new { evaluatedUserId = s.Employee.Id, templateId = s.Template.Id, mensaje = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Clarification_FullFlow_RequestRespondAndReview()
    {
        ClarificationScenario s;
        await using (var db = Factory.CreateDbContext())
        {
            s = await ClarificationTestData.SeedAsync(db);
        }

        var rrhhClient = CreateAuthenticatedClient(s.Rrhh, s.Company);
        var employeeClient = CreateAuthenticatedClient(s.Employee, s.Company);
        var managerClient = CreateAuthenticatedClient(s.Manager, s.Company);

        var created = await rrhhClient.PostAsJsonAsync($"/evaluation-cycles/{s.Cycle.Id}/clarifications",
            new { evaluatedUserId = s.Employee.Id, templateId = s.Template.Id, questionId = s.Question.Id, mensaje = "¿Por qué esa nota?" });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var clarificationId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Factory.FakeEmailService.SentEmails.Select(e => e.To).Should().BeEquivalentTo([s.Employee.Email, s.Manager.Email]);

        var mine = await employeeClient.GetFromJsonAsync<JsonElement>("/clarifications/mine");
        var item = mine.EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("myRole").GetString().Should().Be("Evaluado");
        item.GetProperty("myResponse").ValueKind.Should().Be(JsonValueKind.Null);

        (await employeeClient.PutAsJsonAsync($"/clarifications/{clarificationId}/response", new { respuesta = "Tuve mucha carga" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await managerClient.PutAsJsonAsync($"/clarifications/{clarificationId}/response", new { respuesta = "Faltó a entregas" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var review = await rrhhClient.GetFromJsonAsync<JsonElement>($"/evaluation-cycles/{s.Cycle.Id}/clarifications");
        var reviewed = review.EnumerateArray().Should().ContainSingle().Subject;
        reviewed.GetProperty("evaluatedResponse").GetString().Should().Be("Tuve mucha carga");
        reviewed.GetProperty("managerResponse").GetString().Should().Be("Faltó a entregas");
        reviewed.GetProperty("estado").GetString().Should().Be("Respondida");
    }
}
