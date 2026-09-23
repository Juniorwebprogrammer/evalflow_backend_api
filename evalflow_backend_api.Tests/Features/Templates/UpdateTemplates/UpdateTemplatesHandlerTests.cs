using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Templates.UpdateTemplates;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Templates.UpdateTemplates;

public class UpdateTemplatesHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private UpdateTemplateHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Template BuildTemplate(int companyId, string titulo = "Evaluación") => new()
    {
        Titulo = titulo,
        FechaInicio = Start,
        FechaFin = End,
        EmpresaID = companyId,
    };

    [Fact]
    public async Task Handle_WithInvalidDateRange_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateTemplateRecord(1, "Evaluación", null, End, Start, []), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateTemplateRecord(1, "Evaluación", null, Start, End, []), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateTemplateRecord(1, "Evaluación", null, Start, End, []), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithNonExistentTemplate_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateTemplateRecord(999, "Evaluación", null, Start, End, []), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithTemplateBelongingToAnotherCompany_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        db.Companies.AddRange(company, otherCompany);
        await db.SaveChangesAsync();

        var foreignTemplate = BuildTemplate(otherCompany.Id);
        db.Templates.Add(foreignTemplate);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateTemplateRecord(foreignTemplate.Id, "Nuevo título", null, Start, End, []), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
        (await db.Templates.FindAsync(foreignTemplate.Id))!.Titulo.Should().Be("Evaluación");
    }

    [Fact]
    public async Task Handle_WithOwnTemplate_UpdatesFieldsAndAssignedUsersAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var initialUser = TestDataFactory.CreateUser(company, email: "initial@example.com");
        var newUser = TestDataFactory.CreateUser(company, email: "new@example.com");
        db.Users.AddRange(initialUser, newUser);
        await db.SaveChangesAsync();

        var template = BuildTemplate(company.Id);
        template.UsuariosAsignados.Add(initialUser);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);
        var newStart = Start.AddDays(1);
        var newEnd = End.AddDays(1);

        var result = await sut.Handle(
            new UpdateTemplateRecord(template.Id, "Título actualizado", "Nueva descripción", newStart, newEnd, [newUser.Id]),
            CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);

        var updated = await db.Templates.Include(t => t.UsuariosAsignados).SingleAsync(t => t.Id == template.Id);
        updated.Titulo.Should().Be("Título actualizado");
        updated.Descripcion.Should().Be("Nueva descripción");
        updated.FechaInicio.Should().Be(newStart);
        updated.FechaFin.Should().Be(newEnd);
        updated.UsuariosAsignados.Should().ContainSingle(u => u.Id == newUser.Id);
    }
}
