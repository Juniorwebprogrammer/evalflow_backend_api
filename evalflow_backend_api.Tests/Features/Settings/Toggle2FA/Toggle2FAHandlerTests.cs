using evalflow_backend_api.Features.Settings.Toggle2FA;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Settings.Toggle2FA;

public class Toggle2FAHandlerTests
{
    private static Toogle2FAHandle CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) => new(dbContext);

    [Fact]
    public async Task Handle_WithUnknownUserId_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();

        var sut = CreateSut(db);

        var result = await sut.Handle(new Toggle2FARecord(999, true), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_EnablingTwoFactor_UpdatesUserAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", twoFactorEnabled: false);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new Toggle2FARecord(user.Id, true), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);

        var updated = await db.Users.FindAsync(user.Id);
        updated!.TwoFactorEnabled.Should().BeTrue();
        updated.FechaActualizacion.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_DisablingTwoFactor_ClearsCodeAndExpirationAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", twoFactorEnabled: true);
        user.TwoFactorCode = "123456";
        user.TokenValidacionExpiracion = DateTime.UtcNow.AddMinutes(5);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new Toggle2FARecord(user.Id, false), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);

        var updated = await db.Users.FindAsync(user.Id);
        updated!.TwoFactorEnabled.Should().BeFalse();
        updated.TwoFactorCode.Should().BeNull();
        updated.TokenValidacionExpiracion.Should().BeNull();
    }
}
