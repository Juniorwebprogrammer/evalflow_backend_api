using evalflow_backend_api.Features.Auth.VerifyEmail;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Tests.Features.Auth.VerifyEmail;

public class VerifyEmailHandlerTests
{
    private VerifyEmailHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext);

    [Fact]
    public async Task Handle_WithUnknownToken_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(new VerifyEmailRecord("unknown-token"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithExpiredToken_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", emailVerificado: false);
        user.TokenValidacionEmail = "expired-token";
        user.TokenValidacionExpiracion = DateTime.UtcNow.AddHours(-1);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new VerifyEmailRecord("expired-token"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
        var refreshed = await db.Users.FindAsync(user.Id);
        refreshed!.EmailVerificado.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithAlreadyVerifiedEmail_ReturnsOkWithoutChangingAnything()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", emailVerificado: true);
        user.TokenValidacionEmail = "already-verified-token";
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new VerifyEmailRecord("already-verified-token"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Handle_WithValidToken_MarksEmailAsVerified()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", emailVerificado: false);
        user.TokenValidacionEmail = "valid-token";
        user.TokenValidacionExpiracion = DateTime.UtcNow.AddHours(1);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new VerifyEmailRecord("valid-token"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var refreshed = await db.Users.FindAsync(user.Id);
        refreshed!.EmailVerificado.Should().BeTrue();
        refreshed.TokenValidacionEmail.Should().BeNull();
        refreshed.TokenValidacionExpiracion.Should().BeNull();
    }
}
