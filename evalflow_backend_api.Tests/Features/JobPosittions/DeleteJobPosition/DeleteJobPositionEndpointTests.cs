using System.Net;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.JobPosittions.DeleteJobPosition;

public class DeleteJobPositionEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Delete_JobPosition_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.DeleteAsync("/job-positions/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_JobPosition_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.DeleteAsync("/job-positions/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_JobPosition_WithEmployeeRole_ReturnsForbidden()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, rol: AppRoles.Employee);
        JobPosition position = new() { Nombre = "Engineer", EmpresaID = company.Id };

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            db.JobPositions.Add(position);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.DeleteAsync($"/job-positions/{position.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_JobPosition_WithOwnerRole_DeletesJobPosition()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, rol: AppRoles.Owner);
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

        var response = await client.DeleteAsync($"/job-positions/{position.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.JobPositions.AnyAsync(jp => jp.Id == position.Id)).Should().BeFalse();
    }
}
