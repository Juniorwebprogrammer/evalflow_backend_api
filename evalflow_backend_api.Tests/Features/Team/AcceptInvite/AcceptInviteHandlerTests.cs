using evalflow_backend_api.Features.Team.AcceptInvite;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Team.AcceptInvite;

public class AcceptInviteHandlerTests
{
    private readonly Mock<IPasswordHasser> _passwordHasher = new();

    private AcceptInviteHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _passwordHasher.Object);

    [Fact]
    public async Task Handle_WithUnknownToken_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(new AcceptInviteRecord("does-not-exist", "Ana", "Perez", "Password123"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithExpiredToken_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "invited@example.com", emailVerificado: false);
        user.TokenInvitacion = "expired-token";
        user.TokenInvitacionExpiracion = DateTime.UtcNow.AddDays(-1);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new AcceptInviteRecord("expired-token", "Ana", "Perez", "Password123"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithValidToken_ActivatesUserAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "invited@example.com", emailVerificado: false);
        user.TokenInvitacion = "valid-token";
        user.TokenInvitacionExpiracion = DateTime.UtcNow.AddDays(7);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _passwordHasher.Setup(p => p.Hash("Password123")).Returns("hashed-password-123");

        var sut = CreateSut(db);

        var result = await sut.Handle(new AcceptInviteRecord("valid-token", "Ana", "Perez", "Password123"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);

        var refreshed = await db.Users.SingleAsync(u => u.Id == user.Id);
        refreshed.Nombre.Should().Be("Ana");
        refreshed.Apellidos.Should().Be("Perez");
        refreshed.PasswordHash.Should().Be("hashed-password-123");
        refreshed.EmailVerificado.Should().BeTrue();
        refreshed.TokenInvitacion.Should().BeNull();
        refreshed.TokenInvitacionExpiracion.Should().BeNull();
    }
}
