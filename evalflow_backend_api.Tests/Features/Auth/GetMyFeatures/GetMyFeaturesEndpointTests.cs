using System.Net;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Auth.GetMyFeatures;

public class GetMyFeaturesEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_MyFeatures_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/auth/my-features");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_MyFeatures_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/auth/my-features");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_MyFeatures_WithRoleWithoutFeatures_ReturnsNotFound()
    {
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "employee@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.GetAsync("/auth/my-features");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_MyFeatures_WithMatchingRole_ReturnsOkWithFeatures()
    {
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "employee@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);

            var role = new Role { Name = user.Rol, Description = "Empleado estándar" };
            role.Features.Add(new RoleFeature { Role = role, FeatureCode = "view-templates", Description = "Ver plantillas" });
            db.Roles.Add(role);

            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.GetAsync("/auth/my-features");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
