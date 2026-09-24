using System.Net;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Clarifications.GetMyClarifications;

public class GetMyClarificationsEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_MyClarifications_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/clarifications/mine");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
