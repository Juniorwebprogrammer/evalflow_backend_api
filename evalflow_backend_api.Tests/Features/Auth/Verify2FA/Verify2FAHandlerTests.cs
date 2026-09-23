using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Auth.Login;
using evalflow_backend_api.Features.Auth.Verify2FA;
using evalflow_backend_api.Infrastructure.Jwt;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.Auth.Verify2FA;

public class Verify2FAHandlerTests
{
    private readonly Mock<IJwtProvider> _jwtProvider = new();

    private Verify2FaHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _jwtProvider.Object);

    [Fact]
    public async Task Handle_WithUnknownEmail_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(new Verify2FARecord("nobody@example.com", "123456"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithNoPendingCode_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        user.TwoFactorCode = null;
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new Verify2FARecord("user@example.com", "123456"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithExpiredCode_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        user.TwoFactorCode = "ABC123";
        user.TwoFactorCodeExpiration = DateTime.UtcNow.AddMinutes(-5);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new Verify2FARecord("user@example.com", "ABC123"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithWrongCode_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        user.TwoFactorCode = "ABC123";
        user.TwoFactorCodeExpiration = DateTime.UtcNow.AddMinutes(5);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new Verify2FARecord("user@example.com", "WRONG1"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithValidCode_ConsumesCodeAndReturnsJwt()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        user.TwoFactorCode = "ABC123";
        user.TwoFactorCodeExpiration = DateTime.UtcNow.AddMinutes(5);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _jwtProvider.Setup(j => j.GenerateJwt(It.IsAny<User>(), It.IsAny<Company>())).Returns("fake-jwt");
        _jwtProvider.Setup(j => j.GenerateRefreshToken()).Returns("fake-refresh-token");

        var sut = CreateSut(db);

        // Code comparison is case-insensitive.
        var result = await sut.Handle(new Verify2FARecord("user@example.com", "abc123"), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<LoginResponse>>().Subject;
        ok.Value!.Jwt.Should().Be("fake-jwt");
        ok.Value.RefreshToken.Should().Be("fake-refresh-token");

        var refreshed = await db.Users.FindAsync(user.Id);
        refreshed!.TwoFactorCode.Should().BeNull();
        refreshed.TwoFactorCodeExpiration.Should().BeNull();
    }
}
