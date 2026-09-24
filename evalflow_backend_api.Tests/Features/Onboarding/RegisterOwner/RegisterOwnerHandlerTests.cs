using evalflow_backend_api.Infrastructure.Frontend;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Onboarding.RegisterOwner;
using evalflow_backend_api.Infrastructure.Jwt;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Tests.Features.Onboarding.RegisterOwner;

public class RegisterOwnerHandlerTests
{
    private readonly Mock<IJwtProvider> _jwtProvider = new();
    private readonly Mock<IPasswordHasser> _passwordHasher = new();
    private readonly Mock<IEmailService> _emailService = new();

    private RegisterOwnerHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _jwtProvider.Object, _passwordHasher.Object, _emailService.Object, Options.Create(new FrontendSettings { BaseUrl = "http://frontend.test" }));

    private static RegisterOwnerRecord ValidRequest(string email = "owner@example.com") => new(
        UserNombre: "Ada",
        Apellidos: "Lovelace",
        Email: email,
        Password: "SecurePass1",
        CompanyNombre: "Acme Corp",
        CompanyColors: "#FFFFFF",
        PlanId: 1,
        Cif: "B12345678",
        DireccionFiscal: "Calle Falsa 123",
        Sector: "Tech"
    );

    [Fact]
    public async Task Handle_WithInvalidEmail_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(ValidRequest(email: "not-an-email"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
        (await db.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithShortPassword_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var request = ValidRequest() with { Password = "123" };

        var result = await sut.Handle(request, CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
        (await db.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithExistingEmail_ReturnsConflict()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        var existingUser = TestDataFactory.CreateUser(company, email: "owner@example.com");
        db.Companies.Add(company);
        db.Users.Add(existingUser);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(ValidRequest(email: "owner@example.com"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Handle_WithValidRequest_CreatesCompanyAndOwnerAndSendsWelcomeEmail()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _jwtProvider.Setup(j => j.GenerateJwt(It.IsAny<User>(), It.IsAny<Company>())).Returns("fake-jwt");
        _jwtProvider.Setup(j => j.GenerateRefreshToken()).Returns("fake-refresh-token");
        _passwordHasher.Setup(p => p.Hash("SecurePass1")).Returns("hashed-password");

        var sut = CreateSut(db);

        var result = await sut.Handle(ValidRequest(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<RegisterOwnerResponse>>().Subject;
        ok.Value!.Jwt.Should().Be("fake-jwt");
        ok.Value.RefreshToken.Should().Be("fake-refresh-token");
        ok.Value.UserNombre.Should().Be("Ada");

        var createdUser = await db.Users.Include(u => u.Empresa).SingleAsync();
        createdUser.Email.Should().Be("owner@example.com");
        createdUser.PasswordHash.Should().Be("hashed-password");
        createdUser.Rol.Should().Be(AppRoles.Owner);
        createdUser.Empresa!.Nombre.Should().Be("acme corp");
        createdUser.TokenValidacionEmail.Should().NotBeNullOrEmpty();

        _emailService.Verify(e => e.SendEmailAsync("owner@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidRequest_SeedsDefaultTemplatesForTheNewCompany()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _jwtProvider.Setup(j => j.GenerateJwt(It.IsAny<User>(), It.IsAny<Company>())).Returns("fake-jwt");
        _jwtProvider.Setup(j => j.GenerateRefreshToken()).Returns("fake-refresh-token");
        _passwordHasher.Setup(p => p.Hash("SecurePass1")).Returns("hashed-password");

        var sut = CreateSut(db);

        await sut.Handle(ValidRequest(), CancellationToken.None);

        var createdCompany = await db.Users.Include(u => u.Empresa).Select(u => u.Empresa!).SingleAsync();
        var templateTitles = await db.Templates.Where(t => t.EmpresaID == createdCompany.Id).Select(t => t.Titulo).ToListAsync();

        templateTitles.Should().HaveCount(3);
        templateTitles.Should().Contain("Plantilla Estándar 180° (Solo Mánager)");
        templateTitles.Should().Contain("Plantilla Integral 360°");
        templateTitles.Should().Contain("Plantilla de Autoevaluación");
    }
}
