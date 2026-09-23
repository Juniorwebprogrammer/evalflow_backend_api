using evalflow_backend_api.Features.Profile.ChangePassword;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Profile.ChangePassword;

public class ChangePasswordHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IPasswordHasser> _passwordHasher = new();

    private ChangePasswordHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object, _passwordHasher.Object);

    [Fact]
    public async Task Handle_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new ChangePasswordRecord("old-password", "new-password"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownUserId_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns("999");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ChangePasswordRecord("old-password", "new-password"), CancellationToken.None);

        result.Should().BeOfType<NotFound<string>>();
    }

    [Fact]
    public async Task Handle_WithWrongCurrentPassword_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", passwordHash: "stored-hash");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());
        _passwordHasher.Setup(p => p.Verify("wrong-password", "stored-hash")).Returns(false);

        var sut = CreateSut(db);

        var result = await sut.Handle(new ChangePasswordRecord("wrong-password", "new-password"), CancellationToken.None);

        result.Should().BeOfType<BadRequest<string>>();

        var untouched = await db.Users.FindAsync(user.Id);
        untouched!.PasswordHash.Should().Be("stored-hash");
    }

    [Fact]
    public async Task Handle_WithCorrectCurrentPassword_UpdatesPasswordAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", passwordHash: "stored-hash");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());
        _passwordHasher.Setup(p => p.Verify("correct-password", "stored-hash")).Returns(true);
        _passwordHasher.Setup(p => p.Hash("new-password")).Returns("new-hash");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ChangePasswordRecord("correct-password", "new-password"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);

        var updated = await db.Users.FindAsync(user.Id);
        updated!.PasswordHash.Should().Be("new-hash");
    }
}
