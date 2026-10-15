using evalflow_backend_api.Features.Profile.DeleteAccount;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Profile.DeleteAccount;

public class DeleteAccountHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private DeleteAccountHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteAccountRecord("user@example.com"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownUserId_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns("999");

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteAccountRecord("user@example.com"), CancellationToken.None);

        result.Should().BeOfType<NotFound<string>>();
    }

    [Fact]
    public async Task Handle_WithAnotherEmail_ReturnsBadRequestAndDoesNotDeleteUser()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteAccountRecord("someone@example.com"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);

        (await db.Users.FindAsync(user.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithTheAccountEmail_DeletesUserAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteAccountRecord(" USER@example.com "), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);

        (await db.Users.FindAsync(user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenTheUserPaysForTheCompany_ReturnsConflictAndKeepsTheUser()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1", billingVexxenUserId: "vx-owner");
        var owner = TestDataFactory.CreateUser(company, email: "owner@example.com", vexxenUserId: "vx-owner");
        db.Companies.Add(company);
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(owner.Id.ToString());

        var result = await CreateSut(db).Handle(new DeleteAccountRecord("owner@example.com"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status409Conflict);
        (await db.Users.FindAsync(owner.Id)).Should().NotBeNull();
    }
}
