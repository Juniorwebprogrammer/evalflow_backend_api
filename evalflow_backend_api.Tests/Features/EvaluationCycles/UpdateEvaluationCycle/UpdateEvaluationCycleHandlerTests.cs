using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationCycles.UpdateEvaluationCycle;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationCycles.UpdateEvaluationCycle;

public class UpdateEvaluationCycleHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private UpdateEvaluationCycleHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithFechaInicioNotBeforeFechaFin_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var request = new UpdateEvaluationCycleRecord(
            1, "Q1 Review", null, true, new DateTime(2026, 1, 10), new DateTime(2026, 1, 1));

        var result = await sut.Handle(request, CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);
        var request = new UpdateEvaluationCycleRecord(1, "Q1 Review", null, true, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        var result = await sut.Handle(request, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);
        var request = new UpdateEvaluationCycleRecord(1, "Q1 Review", null, true, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        var result = await sut.Handle(request, CancellationToken.None);

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
        var request = new UpdateEvaluationCycleRecord(999, "Q1 Review", null, true, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        var result = await sut.Handle(request, CancellationToken.None);

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
        var request = new UpdateEvaluationCycleRecord(otherCycle.Id, "Renamed", null, true, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        var result = await sut.Handle(request, CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithValidData_UpdatesCycleAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var cycle = new EvaluationCycle
        {
            Nombre = "Old name",
            Descripcion = "Old description",
            Activo = false,
            FechaInicio = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            FechaFin = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EmpresaID = company.Id,
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);
        var newStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newEnd = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var request = new UpdateEvaluationCycleRecord(
            cycle.Id, "New name", "New description", true, newStart, newEnd, EvaluationType.Evaluacion180);

        var result = await sut.Handle(request, CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var updated = await db.EvaluationCycles.FindAsync(cycle.Id);
        updated!.Nombre.Should().Be("New name");
        updated.Descripcion.Should().Be("New description");
        updated.Activo.Should().BeTrue();
        updated.FechaInicio.Should().Be(newStart.ToUniversalTime());
        updated.FechaFin.Should().Be(newEnd.ToUniversalTime());
        updated.TipoEvaluación.Should().Be(EvaluationType.Evaluacion180);
    }
}
