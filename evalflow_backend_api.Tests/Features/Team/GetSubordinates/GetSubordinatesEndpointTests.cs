using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Team.GetSubordinates;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;

namespace evalflow_backend_api.Tests.Features.Team.GetSubordinates;

public class GetSubordinatesEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_Subordinates_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/team/1/subordinates");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Subordinates_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/team/1/subordinates");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Subordinates_WithValidJwt_ReturnsSubordinateList()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var caller = TestDataFactory.CreateUser(company, email: "caller@example.com");
        var superior = TestDataFactory.CreateUser(company, email: "superior@example.com");
        var subordinate = TestDataFactory.CreateUser(company, email: "subordinate@example.com");
        var position = new JobPosition { Nombre = "Analista", EmpresaID = company.Id, Empresa = company };

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.JobPositions.Add(position);
            db.Users.AddRange(caller, superior, subordinate);
            await db.SaveChangesAsync();
            subordinate.SuperiorId = superior.Id;
            subordinate.CargoId = position.Id;
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(caller, company);

        var response = await client.GetAsync($"/team/{superior.Id}/subordinates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<List<SubordinateDto>>();
        payload.Should().ContainSingle(s => s.Email == "subordinate@example.com");
    }
}
