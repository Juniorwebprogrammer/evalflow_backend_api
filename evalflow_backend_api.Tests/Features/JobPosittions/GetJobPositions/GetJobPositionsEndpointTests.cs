using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.JobPosittions.JobPositionDto;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.JobPosittions.GetJobPositions;

public class GetJobPositionsEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_JobPositions_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/job-positions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_JobPositions_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/job-positions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_JobPositions_WithValidJwt_ReturnsOwnJobPositions()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company);
        JobPosition position;

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // EmpresaID needs the company row id EF just generated above.
            position = new JobPosition { Nombre = "Engineer", EmpresaID = company.Id };
            db.JobPositions.Add(position);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.GetAsync("/job-positions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<List<JobPositionDto>>();
        payload!.Should().ContainSingle(p => p.Nombre == "Engineer");
    }
}
