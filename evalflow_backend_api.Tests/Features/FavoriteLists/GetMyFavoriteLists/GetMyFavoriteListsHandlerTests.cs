using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.FavoriteLists.FavoriteListDto;
using evalflow_backend_api.Features.FavoriteLists.GetMyFavoriteLists;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.FavoriteLists.GetMyFavoriteLists;

public class GetMyFavoriteListsHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetMyFavoriteListsHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutUserClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetMyFavoriteListsRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithValidUser_ReturnsOnlyOwnLists()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        var otherUser = TestDataFactory.CreateUser(company, email: "other@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(user, otherUser);
        await db.SaveChangesAsync();

        db.TemplateFavoriteLists.AddRange(
            new TemplateFavoriteList { Nombre = "Mis favoritos", UserId = user.Id },
            new TemplateFavoriteList { Nombre = "Favoritos ajenos", UserId = otherUser.Id });
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetMyFavoriteListsRecord(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<FavoriteListDto>>>().Subject;
        ok.Value.Should().ContainSingle(l => l.Nombre == "Mis favoritos");
    }
}
