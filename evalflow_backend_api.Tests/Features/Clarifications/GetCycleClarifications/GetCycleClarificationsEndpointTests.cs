using System.Net;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Clarifications.GetCycleClarifications;

public class GetCycleClarificationsEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_CycleClarifications_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/evaluation-cycles/1/clarifications");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_CycleClarifications_WithEmployeeRole_ReturnsForbidden()
    {
        ClarificationScenario s;
        await using (var db = Factory.CreateDbContext())
        {
            s = await ClarificationTestData.SeedAsync(db);
        }

        var client = CreateAuthenticatedClient(s.Employee, s.Company);

        var response = await client.GetAsync($"/evaluation-cycles/{s.Cycle.Id}/clarifications");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
