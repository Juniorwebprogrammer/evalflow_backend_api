using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.FavoriteLists.DeleteFavoriteList;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.FavoriteLists.DeleteFavoriteList;

public class DeleteFavoriteListHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private DeleteFavoriteListHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    private static TemplateFavoriteList BuildList(int userId, string nombre = "Favoritos") => new()
    {
        Nombre = nombre,
        UserId = userId,
    };

    [Fact]
    public async Task Handle_WithoutUserClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteFavoriteListRecord(1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithNonExistentList_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns("1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteFavoriteListRecord(999), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithListOwnedByAnotherUser_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var owner = TestDataFactory.CreateUser(company, email: "owner@example.com");
        var otherUser = TestDataFactory.CreateUser(company, email: "other@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(owner, otherUser);
        await db.SaveChangesAsync();

        var list = BuildList(owner.Id);
        db.TemplateFavoriteLists.Add(list);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(otherUser.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteFavoriteListRecord(list.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
        (await db.TemplateFavoriteLists.FindAsync(list.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithOwnList_DeletesAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var list = BuildList(user.Id);
        db.TemplateFavoriteLists.Add(list);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteFavoriteListRecord(list.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        (await db.TemplateFavoriteLists.FindAsync(list.Id)).Should().BeNull();
    }
}
