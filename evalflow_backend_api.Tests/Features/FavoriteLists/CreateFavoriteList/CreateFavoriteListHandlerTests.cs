using evalflow_backend_api.Features.FavoriteLists.CreateFavoriteList;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.FavoriteLists.CreateFavoriteList;

public class CreateFavoriteListHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private CreateFavoriteListHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutUserClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateFavoriteListRecord("Favoritos", null), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithNonNumericUserClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns("not-a-number");

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateFavoriteListRecord("Favoritos", null), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithValidUser_CreatesFavoriteListAndReturnsCreated()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateFavoriteListRecord("Mis favoritos", "Lista personal"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status201Created);

        var list = await db.TemplateFavoriteLists.SingleAsync();
        list.Nombre.Should().Be("Mis favoritos");
        list.Descripcion.Should().Be("Lista personal");
        list.UserId.Should().Be(user.Id);
    }
}
