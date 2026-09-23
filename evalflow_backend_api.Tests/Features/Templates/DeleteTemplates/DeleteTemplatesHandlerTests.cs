using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Templates.DeleteTemplate;
using evalflow_backend_api.Features.Templates.DeleteTemplates;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Templates.DeleteTemplates;

public class DeleteTemplatesHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private DeleteTemplateHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    private static Template BuildTemplate(int companyId, string titulo = "Evaluación") => new()
    {
        Titulo = titulo,
        FechaInicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        FechaFin = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
        EmpresaID = companyId,
    };

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteTemplateRecord(1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteTemplateRecord(1), CancellationToken.None);

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

        var result = await sut.Handle(new DeleteTemplateRecord(999), CancellationToken.None);

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

        var result = await sut.Handle(new DeleteTemplateRecord(foreignTemplate.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
        (await db.Templates.FindAsync(foreignTemplate.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithOwnTemplate_DeletesAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var template = BuildTemplate(company.Id);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteTemplateRecord(template.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        (await db.Templates.FindAsync(template.Id)).Should().BeNull();
    }
}
