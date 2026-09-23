using System.Net;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Companies.GetByIdentificationId;

public class GetCompanyByIdentificationIdEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_CompanyByIdentificationId_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/Company/get/by-identification/tenant-1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_CompanyByIdentificationId_WhenNotFound_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/Company/get/by-identification/unknown-tenant");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_CompanyByIdentificationId_WhenFound_ReturnsOkWithCompany()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1", nombre: "Acme Corp");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync("/Company/get/by-identification/tenant-1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
