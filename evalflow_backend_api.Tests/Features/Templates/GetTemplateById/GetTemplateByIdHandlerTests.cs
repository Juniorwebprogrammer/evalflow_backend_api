using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Templates.GetTemplateById;
using evalflow_backend_api.Features.Templates.TemplatesDto;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.Templates.GetTemplateById;

public class GetTemplateByIdHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetTemplateByIdHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
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

        var result = await sut.Handle(new GetTemplateByIdRecord(1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetTemplateByIdRecord(1), CancellationToken.None);

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

        var result = await sut.Handle(new GetTemplateByIdRecord(999), CancellationToken.None);

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

        var result = await sut.Handle(new GetTemplateByIdRecord(foreignTemplate.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithOwnTemplate_ReturnsOkWithDetails()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var template = BuildTemplate(company.Id, "Evaluación de desempeño");
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetTemplateByIdRecord(template.Id), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<TemplateDetailsDto>>().Subject;
        ok.Value!.Id.Should().Be(template.Id);
        ok.Value.Titulo.Should().Be("Evaluación de desempeño");
    }
}
