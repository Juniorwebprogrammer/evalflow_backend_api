using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.FavoriteLists.FavoriteListDto;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;

namespace evalflow_backend_api.Tests.Features.FavoriteLists.GetMyFavoriteLists;

public class GetMyFavoriteListsEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_FavoriteLists_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/favorite-lists");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_FavoriteLists_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/favorite-lists");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_FavoriteLists_WithValidJwt_ReturnsOwnLists()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        var otherUser = TestDataFactory.CreateUser(company, email: "other@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.AddRange(user, otherUser);
            await db.SaveChangesAsync();

            db.TemplateFavoriteLists.AddRange(
                new TemplateFavoriteList { Nombre = "Mis favoritos", UserId = user.Id },
                new TemplateFavoriteList { Nombre = "Favoritos ajenos", UserId = otherUser.Id });
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.GetAsync("/favorite-lists");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<List<FavoriteListDto>>();
        payload.Should().ContainSingle(l => l.Nombre == "Mis favoritos");
    }
}
