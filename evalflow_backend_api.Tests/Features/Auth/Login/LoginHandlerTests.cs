using evalflow_backend_api.Features.Auth.Login;
using evalflow_backend_api.Infrastructure.Jwt;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.Auth.Login;

public class LoginHandlerTests
{
    private readonly Mock<IJwtProvider> _jwtProvider = new();
    private readonly Mock<IPasswordHasser> _passwordHasher = new();
    private readonly Mock<IEmailService> _emailService = new();

    private LoginHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _jwtProvider.Object, _passwordHasher.Object, _emailService.Object);

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsJwtAndRefreshToken()
    {
        // Arrange
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", passwordHash: "stored-hash");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _passwordHasher.Setup(p => p.Verify("correct-password", "stored-hash")).Returns(true);
        _jwtProvider.Setup(j => j.GenerateJwt(It.IsAny<evalflow_backend_api.Domain.Entities.User>(), It.IsAny<evalflow_backend_api.Domain.Entities.Company>()))
            .Returns("fake-jwt");
        _jwtProvider.Setup(j => j.GenerateRefreshToken()).Returns("fake-refresh-token");

        var sut = CreateSut(db);
        var request = new LoginRecord("user@example.com", "correct-password", "tenant-1");

        // Act
        var result = await sut.Handle(request, CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<Ok<LoginResponse>>().Subject;
        ok.Value!.Jwt.Should().Be("fake-jwt");
        ok.Value.RefreshToken.Should().Be("fake-refresh-token");
        ok.Value.TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", passwordHash: "stored-hash");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _passwordHasher.Setup(p => p.Verify(It.IsAny<string>(), "stored-hash")).Returns(false);

        var sut = CreateSut(db);
        var request = new LoginRecord("user@example.com", "wrong-password", "tenant-1");

        var result = await sut.Handle(request, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownEmail_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);
        var request = new LoginRecord("nobody@example.com", "whatever", "tenant-1");

        var result = await sut.Handle(request, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
        _passwordHasher.Verify(p => p.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithMismatchedTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", passwordHash: "stored-hash");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _passwordHasher.Setup(p => p.Verify(It.IsAny<string>(), "stored-hash")).Returns(true);

        var sut = CreateSut(db);
        var request = new LoginRecord("user@example.com", "correct-password", "some-other-tenant");

        var result = await sut.Handle(request, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnverifiedEmail_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", passwordHash: "stored-hash", emailVerificado: false);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _passwordHasher.Setup(p => p.Verify(It.IsAny<string>(), "stored-hash")).Returns(true);

        var sut = CreateSut(db);
        var request = new LoginRecord("user@example.com", "correct-password", "tenant-1");

        var result = await sut.Handle(request, CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithTwoFactorEnabled_SendsCodeAndDoesNotReturnJwt()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", passwordHash: "stored-hash", twoFactorEnabled: true);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _passwordHasher.Setup(p => p.Verify(It.IsAny<string>(), "stored-hash")).Returns(true);

        var sut = CreateSut(db);
        var request = new LoginRecord("user@example.com", "correct-password", "tenant-1");

        var result = await sut.Handle(request, CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        _emailService.Verify(e => e.SendEmailAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _jwtProvider.Verify(j => j.GenerateJwt(It.IsAny<evalflow_backend_api.Domain.Entities.User>(), It.IsAny<evalflow_backend_api.Domain.Entities.Company>()), Times.Never);

        var refreshed = await db.Users.FindAsync(user.Id);
        refreshed!.TwoFactorCode.Should().NotBeNullOrEmpty();
    }
}
