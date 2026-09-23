using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.JobPosittions.CreateJobPosition;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.JobPosittions.CreateJobPosition;

public class CreateJobPositionEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_JobPositions_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync("/job-positions", new CreateJobPositionRecord("Engineer", null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_JobPositions_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/job-positions", new CreateJobPositionRecord("Engineer", null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_JobPositions_WithEmployeeRole_ReturnsForbidden()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, rol: AppRoles.Employee);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PostAsJsonAsync("/job-positions", new CreateJobPositionRecord("Engineer", null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_JobPositions_WithOwnerRole_CreatesJobPosition()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, rol: AppRoles.Owner);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PostAsJsonAsync("/job-positions", new CreateJobPositionRecord("Engineer", "Builds software"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.JobPositions.SingleAsync()).Nombre.Should().Be("Engineer");
    }
}
