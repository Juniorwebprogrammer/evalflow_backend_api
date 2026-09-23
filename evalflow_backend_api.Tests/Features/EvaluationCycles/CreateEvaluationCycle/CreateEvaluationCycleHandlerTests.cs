using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationCycles.CreateEvaluationCycle;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationCycles.CreateEvaluationCycle;

public class CreateEvaluationCycleHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private CreateEvaluationCycleHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithFechaInicioNotBeforeFechaFin_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var request = new CreateEvaluationCycleRecord(
            "Q1 Review", "desc", new DateTime(2026, 1, 10), new DateTime(2026, 1, 1));

        var result = await sut.Handle(request, CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);
        var request = new CreateEvaluationCycleRecord("Q1 Review", null, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        var result = await sut.Handle(request, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);
        var request = new CreateEvaluationCycleRecord("Q1 Review", null, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        var result = await sut.Handle(request, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithValidData_CreatesCycleAsInactiveAndReturnsCreated()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var request = new CreateEvaluationCycleRecord("Q1 Review", "Quarterly review", start, end);

        var result = await sut.Handle(request, CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status201Created);
        var cycle = await db.EvaluationCycles.SingleAsync();
        cycle.Nombre.Should().Be("Q1 Review");
        cycle.Descripcion.Should().Be("Quarterly review");
        cycle.EmpresaID.Should().Be(company.Id);
        cycle.Activo.Should().BeFalse();
        cycle.FechaInicio.Should().Be(start.ToUniversalTime());
        cycle.FechaFin.Should().Be(end.ToUniversalTime());
        cycle.TipoEvaluación.Should().Be(EvaluationType.Evaluacion360);
    }

    [Fact]
    public async Task Handle_WithExplicitEvaluationType_PersistsIt()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);
        var request = new CreateEvaluationCycleRecord(
            "Auto only", null, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1), EvaluationType.Auto);

        await sut.Handle(request, CancellationToken.None);

        var cycle = await db.EvaluationCycles.SingleAsync();
        cycle.TipoEvaluación.Should().Be(EvaluationType.Auto);
    }
}
