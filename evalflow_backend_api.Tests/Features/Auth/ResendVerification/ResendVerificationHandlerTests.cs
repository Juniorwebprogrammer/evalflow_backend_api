using evalflow_backend_api.Infrastructure.Frontend;
using evalflow_backend_api.Features.Auth.ResendVerification;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Moq;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Tests.Features.Auth.ResendVerification;

public class ResendVerificationHandlerTests
{
    private readonly Mock<IEmailService> _emailService = new();

    private ResendVerificationHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _emailService.Object, Options.Create(new FrontendSettings { BaseUrl = "http://frontend.test" }));

    [Fact]
    public async Task Handle_WithUnknownEmail_ReturnsOkAndSendsNoEmail()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(new ResendVerificationRecord("nobody@example.com"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        _emailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAlreadyVerifiedEmail_ReturnsOkAndSendsNoEmail()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "verified@example.com", emailVerificado: true);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new ResendVerificationRecord("verified@example.com"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        _emailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithUnverifiedEmail_RefreshesTokenAndSendsEmail()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "pending@example.com", emailVerificado: false);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new ResendVerificationRecord("pending@example.com"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var refreshed = await db.Users.FindAsync(user.Id);
        refreshed!.TokenValidacionEmail.Should().NotBeNullOrEmpty();
        refreshed.TokenValidacionExpiracion.Should().NotBeNull();
        _emailService.Verify(e => e.SendEmailAsync("pending@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
