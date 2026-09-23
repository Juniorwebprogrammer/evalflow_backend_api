using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.FavoriteLists.UpdateFavoriteList;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.FavoriteLists.UpdateFavoriteList;

public class UpdateFavoriteListHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private UpdateFavoriteListHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutUserClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateFavoriteListRecord(1, "Favoritos", null), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithNonExistentList_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns("1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateFavoriteListRecord(999, "Favoritos", null), CancellationToken.None);

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

        var list = new TemplateFavoriteList { Nombre = "Favoritos", UserId = owner.Id };
        db.TemplateFavoriteLists.Add(list);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(otherUser.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateFavoriteListRecord(list.Id, "Nuevo nombre", null), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
        (await db.TemplateFavoriteLists.FindAsync(list.Id))!.Nombre.Should().Be("Favoritos");
    }

    [Fact]
    public async Task Handle_WithOwnList_UpdatesFieldsAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var list = new TemplateFavoriteList { Nombre = "Favoritos", Descripcion = "Original", UserId = user.Id };
        db.TemplateFavoriteLists.Add(list);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateFavoriteListRecord(list.Id, "Nombre actualizado", "Descripción nueva"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var updated = await db.TemplateFavoriteLists.FindAsync(list.Id);
        updated!.Nombre.Should().Be("Nombre actualizado");
        updated.Descripcion.Should().Be("Descripción nueva");
    }
}
