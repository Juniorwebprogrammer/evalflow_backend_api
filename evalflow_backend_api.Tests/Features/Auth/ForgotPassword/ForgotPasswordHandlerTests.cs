using evalflow_backend_api.Features.Auth.ForgotPassword;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.Auth.ForgotPassword;

public class ForgotPasswordHandlerTests
{
    private readonly Mock<IEmailService> _emailService = new();

    private ForgotPasswordHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _emailService.Object);

    [Fact]
    public async Task Handle_WithUnknownEmail_ReturnsOkAndSendsNoEmail()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(new ForgotPasswordRecord("nobody@example.com"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        _emailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInactiveUser_ReturnsOkAndSendsNoEmail()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "inactive@example.com", activo: false);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new ForgotPasswordRecord("inactive@example.com"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        _emailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        var refreshed = await db.Users.FindAsync(user.Id);
        refreshed!.TokenRecuperacionPassword.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithActiveUser_GeneratesTokenAndSendsEmail()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "active@example.com");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new ForgotPasswordRecord("active@example.com"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var refreshed = await db.Users.FindAsync(user.Id);
        refreshed!.TokenRecuperacionPassword.Should().NotBeNullOrEmpty();
        refreshed.ExpiracionTokenRecuperacion.Should().NotBeNull();
        _emailService.Verify(e => e.SendEmailAsync("active@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmailInDifferentCase_StillMatchesUser()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "MixedCase@Example.com");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new ForgotPasswordRecord("mixedcase@example.com"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        _emailService.Verify(e => e.SendEmailAsync("MixedCase@Example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
