using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Clarifications.RespondClarification;

public class RespondClarificationEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_Response_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync("/clarifications/1/response", new { respuesta = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_Response_ForClarificationOfAnotherUser_ReturnsNotFound()
    {
        ClarificationScenario s;
        int clarificationId;
        await using (var db = Factory.CreateDbContext())
        {
            s = await ClarificationTestData.SeedAsync(db);
            clarificationId = (await ClarificationTestData.AddClarificationAsync(db, s)).Id;
        }

        var client = CreateAuthenticatedClient(s.Rrhh, s.Company);

        var response = await client.PutAsJsonAsync($"/clarifications/{clarificationId}/response", new { respuesta = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
