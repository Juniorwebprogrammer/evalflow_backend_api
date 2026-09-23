using evalflow_backend_api.Features.Auth.Resend2FA;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Moq;

namespace evalflow_backend_api.Tests.Features.Auth.Resend2FA;

public class Resend2FAHandlerTests
{
    private readonly Mock<IEmailService> _emailService = new();

    private Resend2FAHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _emailService.Object);

    [Fact]
    public async Task Handle_WithUnknownEmail_ReturnsOkAndSendsNoEmail()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(new Resend2FARecord("nobody@example.com"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        _emailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithTwoFactorDisabled_ReturnsOkAndSendsNoEmail()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", twoFactorEnabled: false);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new Resend2FARecord("user@example.com"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        _emailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithTwoFactorEnabled_RefreshesCodeAndSendsEmail()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", twoFactorEnabled: true);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new Resend2FARecord("user@example.com"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var refreshed = await db.Users.FindAsync(user.Id);
        refreshed!.TwoFactorCode.Should().NotBeNullOrEmpty();
        refreshed.TwoFactorCodeExpiration.Should().NotBeNull();
        _emailService.Verify(e => e.SendEmailAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
