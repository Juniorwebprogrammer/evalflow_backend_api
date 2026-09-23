using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.FavoriteLists.CreateFavoriteList;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.FavoriteLists.CreateFavoriteList;

public class CreateFavoriteListEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_FavoriteLists_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync("/favorite-lists", new CreateFavoriteListBody("Favoritos", null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_FavoriteLists_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/favorite-lists", new CreateFavoriteListBody("Favoritos", null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_FavoriteLists_WithValidJwt_CreatesFavoriteList()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PostAsJsonAsync("/favorite-lists", new CreateFavoriteListBody("Mis favoritos", "Lista personal"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.TemplateFavoriteLists.SingleAsync()).Nombre.Should().Be("Mis favoritos");
    }
}
