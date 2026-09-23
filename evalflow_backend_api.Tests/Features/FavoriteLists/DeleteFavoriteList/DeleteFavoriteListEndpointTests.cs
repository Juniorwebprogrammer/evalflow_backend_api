using System.Net;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;

namespace evalflow_backend_api.Tests.Features.FavoriteLists.DeleteFavoriteList;

public class DeleteFavoriteListEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Delete_FavoriteLists_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.DeleteAsync("/favorite-lists/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_FavoriteLists_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.DeleteAsync("/favorite-lists/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_FavoriteLists_WithValidJwt_DeletesOwnList()
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

        var response = await client.DeleteAsync($"/favorite-lists/{list.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.TemplateFavoriteLists.FindAsync(list.Id)).Should().BeNull();
    }
}
