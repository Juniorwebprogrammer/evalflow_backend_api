using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.FavoriteLists.UpdateFavoriteList;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;

namespace evalflow_backend_api.Tests.Features.FavoriteLists.UpdateFavoriteList;

public class UpdateFavoriteListEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_FavoriteLists_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsJsonAsync("/favorite-lists/1", new UpdateFavoriteListBody("Favoritos", null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_FavoriteLists_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync("/favorite-lists/1", new UpdateFavoriteListBody("Favoritos", null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_FavoriteLists_WithValidJwt_UpdatesOwnList()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        TemplateFavoriteList list;

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            list = new TemplateFavoriteList { Nombre = "Favoritos", UserId = user.Id };
            db.TemplateFavoriteLists.Add(list);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PutAsJsonAsync($"/favorite-lists/{list.Id}", new UpdateFavoriteListBody("Nombre actualizado", "Nueva descripción"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.TemplateFavoriteLists.FindAsync(list.Id))!.Nombre.Should().Be("Nombre actualizado");
    }
}
