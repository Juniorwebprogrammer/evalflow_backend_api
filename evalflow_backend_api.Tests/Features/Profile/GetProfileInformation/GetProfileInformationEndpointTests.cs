using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Profile.GetProfileInformation;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;

namespace evalflow_backend_api.Tests.Features.Profile.GetProfileInformation;

public class GetProfileInformationEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_Profile_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/Profile/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Profile_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/Profile/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Profile_WithValidJwt_ReturnsProfileInformation()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1", nombre: "Acme Corp");
        var user = TestDataFactory.CreateUser(company, email: "owner@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.GetAsync("/Profile/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<GetProfileInformationDTO>();
        payload!.Email.Should().Be("owner@example.com");
        payload.NombreEmpresa.Should().Be("Acme Corp");
        payload.IdentificationId.Should().Be("tenant-1");
    }
}
