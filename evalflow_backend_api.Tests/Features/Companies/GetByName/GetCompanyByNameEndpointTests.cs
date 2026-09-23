using System.Net;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Companies.GetByName;

public class GetCompanyByNameEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_CompanyByName_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/Company/get/by-name/Acme%20Corp");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_CompanyByName_WhenNotFound_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/Company/get/by-name/Unknown%20Corp");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_CompanyByName_WhenFound_ReturnsOkWithCompany()
    {
        var company = TestDataFactory.CreateCompany(nombre: "Acme Corp", identificationId: "tenant-1");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync("/Company/get/by-name/Acme%20Corp");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
