using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Profile.UpdateProfileInformation;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Profile.UpdateProfileInformation;

public class UpdateProfileInformationEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_UpdateProfile_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsJsonAsync("/Profile/update", new UpdateProfileInformationRecord("Jane", "Doe"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_UpdateProfile_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync("/Profile/update", new UpdateProfileInformationRecord("Jane", "Doe"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_UpdateProfile_WithValidJwt_UpdatesUser()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "owner@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PutAsJsonAsync("/Profile/update", new UpdateProfileInformationRecord("Jane", "Doe"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        var updated = await verifyDb.Users.SingleAsync(u => u.Id == user.Id);
        updated.Nombre.Should().Be("Jane");
        updated.Apellidos.Should().Be("Doe");
    }
}
