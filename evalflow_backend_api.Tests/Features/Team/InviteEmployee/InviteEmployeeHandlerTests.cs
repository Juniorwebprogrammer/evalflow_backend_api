using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.Team.InviteEmployee;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Team.InviteEmployee;

public class InviteEmployeeHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IPasswordHasser> _passwordHasher = new();
    private readonly Mock<IEmailService> _emailService = new();

    private InviteEmployeeHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object, _passwordHasher.Object, _emailService.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new InviteEmployeeRecord("Ana", "Perez", "ana@example.com", AppRoles.Employee), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new InviteEmployeeRecord("Ana", "Perez", "not-an-email", AppRoles.Employee), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithInvalidRole_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new InviteEmployeeRecord("Ana", "Perez", "ana@example.com", "SuperAdmin"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithExistingEmail_ReturnsConflict()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var existingUser = TestDataFactory.CreateUser(company, email: "ana@example.com");
        db.Companies.Add(company);
        db.Users.Add(existingUser);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new InviteEmployeeRecord("Ana", "Perez", "ana@example.com", AppRoles.Employee), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Handle_WithUnknownTenantCompany_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("does-not-exist");

        var sut = CreateSut(db);

        var result = await sut.Handle(new InviteEmployeeRecord("Ana", "Perez", "ana@example.com", AppRoles.Employee), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithValidRequest_CreatesEmployeeSendsInvitationEmailAndReturnsCreated()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1", nombre: "Acme Corp");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");
        _passwordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed-temp-password");

        var sut = CreateSut(db);

        var result = await sut.Handle(new InviteEmployeeRecord("Ana", "Perez", "ana@example.com", AppRoles.Employee), CancellationToken.None);

        var created = result.Should().BeOfType<Created<InviteEmployeeResponse>>().Subject;
        created.Value!.Email.Should().Be("ana@example.com");
        created.Value.RolAsignado.Should().Be(AppRoles.Employee);

        var savedUser = await db.Users.SingleAsync(u => u.Email == "ana@example.com");
        savedUser.EmpresaID.Should().Be(company.Id);
        savedUser.PasswordHash.Should().Be("hashed-temp-password");
        savedUser.EmailVerificado.Should().BeFalse();
        savedUser.TokenInvitacion.Should().NotBeNullOrEmpty();
        savedUser.TokenInvitacionExpiracion.Should().NotBeNull();

        _emailService.Verify(e => e.SendEmailAsync("ana@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
