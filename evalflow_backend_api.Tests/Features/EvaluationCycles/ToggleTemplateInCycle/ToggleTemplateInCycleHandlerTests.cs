using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.EvaluationCycles.ToggleTemplateInCycle;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationCycles.ToggleTemplateInCycle;

public class ToggleTemplateInCycleHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private ToggleTemplateInCycleHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleTemplateInCycleRecord(1, 1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownCycle_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleTemplateInCycleRecord(999, 1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithCycleFromAnotherTenant_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var ownCompany = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        db.Companies.AddRange(ownCompany, otherCompany);
        await db.SaveChangesAsync();

        var otherCycle = new EvaluationCycle
        {
            Nombre = "Other tenant cycle",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(1),
            EmpresaID = otherCompany.Id,
        };
        db.EvaluationCycles.Add(otherCycle);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleTemplateInCycleRecord(otherCycle.Id, 1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithUnknownTemplate_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var cycle = new EvaluationCycle
        {
            Nombre = "Q1 Review",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(1),
            EmpresaID = company.Id,
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleTemplateInCycleRecord(cycle.Id, 999), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithTemplateFromAnotherTenant_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var ownCompany = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        db.Companies.AddRange(ownCompany, otherCompany);
        await db.SaveChangesAsync();

        var cycle = new EvaluationCycle
        {
            Nombre = "Q1 Review",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(1),
            EmpresaID = ownCompany.Id,
        };
        var otherTemplate = new Template
        {
            Titulo = "Other tenant template",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(1),
            EmpresaID = otherCompany.Id,
        };
        db.EvaluationCycles.Add(cycle);
        db.Templates.Add(otherTemplate);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleTemplateInCycleRecord(cycle.Id, otherTemplate.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithTemplateNotYetInCycle_AddsTemplateAndReturnsOk()
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
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(1),
            EmpresaID = company.Id,
        };
        db.Templates.Add(template);
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleTemplateInCycleRecord(cycle.Id, template.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var reloaded = await db.EvaluationCycles.Include(c => c.Templates).SingleAsync(c => c.Id == cycle.Id);
        reloaded.Templates.Should().ContainSingle(t => t.Id == template.Id);
    }

    [Fact]
    public async Task Handle_WithTemplateAlreadyInCycle_RemovesTemplateAndReturnsOk()
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
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(1),
            EmpresaID = company.Id,
            Templates = new List<Template> { template },
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleTemplateInCycleRecord(cycle.Id, template.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var reloaded = await db.EvaluationCycles.Include(c => c.Templates).SingleAsync(c => c.Id == cycle.Id);
        reloaded.Templates.Should().BeEmpty();
    }
}
