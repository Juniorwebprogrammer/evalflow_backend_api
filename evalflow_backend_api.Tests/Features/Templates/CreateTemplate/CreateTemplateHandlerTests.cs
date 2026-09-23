using evalflow_backend_api.Features.Templates.CreateTemplate;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Templates.CreateTemplate;

public class CreateTemplateHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private CreateTemplateHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WithInvalidDateRange_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateTemplateRecord("Evaluación", null, End, Start, []), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateTemplateRecord("Evaluación", null, Start, End, []), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateTemplateRecord("Evaluación", null, Start, End, []), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithValidTenant_CreatesTemplateWithOnlyOwnCompanyUsersAndReturnsCreated()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        db.Companies.AddRange(company, otherCompany);
        await db.SaveChangesAsync();

        var ownUser = TestDataFactory.CreateUser(company, email: "own@example.com");
        var foreignUser = TestDataFactory.CreateUser(otherCompany, email: "foreign@example.com");
        db.Users.AddRange(ownUser, foreignUser);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(
            new CreateTemplateRecord("Evaluación anual", "Descripción", Start, End, [ownUser.Id, foreignUser.Id]),
            CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status201Created);

        var template = await db.Templates.Include(t => t.UsuariosAsignados).SingleAsync();
        template.Titulo.Should().Be("Evaluación anual");
        template.EmpresaID.Should().Be(company.Id);
        template.UsuariosAsignados.Should().ContainSingle(u => u.Id == ownUser.Id);
    }
}
