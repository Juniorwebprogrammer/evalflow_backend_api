using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.EvaluationCycles;
using evalflow_backend_api.Features.EvaluationCycles.GetAllEvaluationCycles;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationCycles.GetAllEvaluationCycles;

public class GetAllEvaluationCyclesHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetAllEvaluationCyclesHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetAllEvaluationCyclesRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetAllEvaluationCyclesRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithValidTenant_ReturnsOnlyOwnCompanyCyclesOrderedByFechaInicioDescending()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var ownCompany = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        db.Companies.AddRange(ownCompany, otherCompany);
        await db.SaveChangesAsync();

        var older = new EvaluationCycle
        {
            Nombre = "Older cycle",
            Activo = false,
            FechaInicio = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            FechaFin = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EmpresaID = ownCompany.Id,
        };
        var newer = new EvaluationCycle
        {
            Nombre = "Newer cycle",
            Activo = true,
            FechaInicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            FechaFin = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EmpresaID = ownCompany.Id,
        };
        var otherTenantCycle = new EvaluationCycle
        {
            Nombre = "Other tenant cycle",
            Activo = false,
            FechaInicio = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            FechaFin = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EmpresaID = otherCompany.Id,
        };
        db.EvaluationCycles.AddRange(older, newer, otherTenantCycle);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetAllEvaluationCyclesRecord(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<EvaluationCycleSummaryDto>>>().Subject;
        ok.Value.Should().HaveCount(2);
        ok.Value!.Select(c => c.Nombre).Should().ContainInOrder("Newer cycle", "Older cycle");
        ok.Value.Should().OnlyContain(c => c.Nombre != "Other tenant cycle");
    }

    [Fact]
    public async Task Handle_WithTemplatesInCycle_ReturnsTemplatesCountAndIds()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var template = new Template
        {
            Titulo = "Peer Review",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(1),
            EmpresaID = company.Id,
        };
        var cycle = new EvaluationCycle
        {
            Nombre = "Q1 Review",
            Activo = false,
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(1),
            EmpresaID = company.Id,
            Templates = new List<Template> { template },
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetAllEvaluationCyclesRecord(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<EvaluationCycleSummaryDto>>>().Subject;
        var dto = ok.Value!.Single();
        dto.TemplatesCount.Should().Be(1);
        dto.TemplateIds.Should().ContainSingle().Which.Should().Be(template.Id);
    }
}
