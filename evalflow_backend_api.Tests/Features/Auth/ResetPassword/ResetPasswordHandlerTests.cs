using evalflow_backend_api.Features.Auth.ResetPassword;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Moq;

namespace evalflow_backend_api.Tests.Features.Auth.ResetPassword;

public class ResetPasswordHandlerTests
{
    private readonly Mock<IPasswordHasser> _passwordHasher = new();

    private ResetPasswordHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _passwordHasher.Object);

    [Fact]
    public async Task Handle_WithUnknownToken_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(new ResetPasswordRecord("unknown-token", "NewPassword123"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
        _passwordHasher.Verify(p => p.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithExpiredToken_ReturnsBadRequestAndInvalidatesToken()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        user.TokenRecuperacionPassword = "expired-token";
        user.ExpiracionTokenRecuperacion = DateTime.UtcNow.AddHours(-1);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new ResetPasswordRecord("expired-token", "NewPassword123"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
        var refreshed = await db.Users.FindAsync(user.Id);
        refreshed!.TokenRecuperacionPassword.Should().BeNull();
        refreshed.ExpiracionTokenRecuperacion.Should().BeNull();
        _passwordHasher.Verify(p => p.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithMissingExpirationDate_TreatsTokenAsExpired()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        user.TokenRecuperacionPassword = "no-expiration-token";
        user.ExpiracionTokenRecuperacion = null;
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new ResetPasswordRecord("no-expiration-token", "NewPassword123"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithValidToken_UpdatesPasswordAndInvalidatesToken()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", passwordHash: "old-hash");
        user.TokenRecuperacionPassword = "valid-token";
        user.ExpiracionTokenRecuperacion = DateTime.UtcNow.AddMinutes(30);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _passwordHasher.Setup(p => p.Hash("NewPassword123")).Returns("new-hash");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ResetPasswordRecord("valid-token", "NewPassword123"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var refreshed = await db.Users.FindAsync(user.Id);
        refreshed!.PasswordHash.Should().Be("new-hash");
        refreshed.TokenRecuperacionPassword.Should().BeNull();
        refreshed.ExpiracionTokenRecuperacion.Should().BeNull();
    }
}
