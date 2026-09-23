using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Templates.GetAllTemplates;
using evalflow_backend_api.Features.Templates.TemplatesDto;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.Templates.GetAllTemplates;

public class GetAllTemplatesHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetAllTemplatesHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    private static Template BuildTemplate(int companyId, string titulo) => new()
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

        var result = await sut.Handle(new GetAllTemplatesRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetAllTemplatesRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithValidTenant_ReturnsOnlyOwnCompanyTemplates()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        db.Companies.AddRange(company, otherCompany);
        await db.SaveChangesAsync();

        var ownTemplate = BuildTemplate(company.Id, "Evaluación propia");
        var foreignTemplate = BuildTemplate(otherCompany.Id, "Evaluación ajena");
        db.Templates.AddRange(ownTemplate, foreignTemplate);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetAllTemplatesRecord(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<TemplateSummaryDto>>>().Subject;
        ok.Value.Should().ContainSingle(t => t.Titulo == "Evaluación propia");
    }
}
