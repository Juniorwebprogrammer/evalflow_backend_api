using evalflow_backend_api.Features.Profile.GetProfileInformation;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.Profile.GetProfileInformation;

public class GetProfileInformationHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetProfileInformationHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetProfileInformationRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownUserId_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns("999");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetProfileInformationRecord(), CancellationToken.None);

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WithNonNumericUserId_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns("not-a-number");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetProfileInformationRecord(), CancellationToken.None);

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WithValidUser_ReturnsProfileInformation()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1", nombre: "Acme Corp");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com", twoFactorEnabled: true);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetProfileInformationRecord(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<GetProfileInformationDTO>>().Subject;
        ok.Value!.Nombre.Should().Be(user.Nombre);
        ok.Value.Apellidos.Should().Be(user.Apellidos);
        ok.Value.Email.Should().Be("user@example.com");
        ok.Value.Rol.Should().Be(user.Rol);
        ok.Value.NombreEmpresa.Should().Be("Acme Corp");
        ok.Value.IdentificationId.Should().Be("tenant-1");
        ok.Value.TwoFactorAuthentication.Should().BeTrue();
    }
}
