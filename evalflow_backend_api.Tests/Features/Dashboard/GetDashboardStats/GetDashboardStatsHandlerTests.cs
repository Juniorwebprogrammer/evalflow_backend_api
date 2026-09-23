using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Dashboard.GetDashboardStats;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.Dashboard.GetDashboardStats;

public class GetDashboardStatsHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetDashboardStatsHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetDashboardStatsRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithNoActiveCycle_ReturnsCountsWithoutActiveCycle()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var activeUser = TestDataFactory.CreateUser(company, email: "active@example.com", activo: true);
        var inactiveUser = TestDataFactory.CreateUser(company, email: "inactive@example.com", activo: false);
        db.Companies.Add(company);
        db.Users.AddRange(activeUser, inactiveUser);
        db.Departments.Add(new Department { Nombre = "Ventas", EmpresaID = company.Id, Empresa = company });

        // A cycle whose date range is already over shouldn't count as active.
        db.EvaluationCycles.Add(new EvaluationCycle
        {
            Nombre = "Ciclo pasado",
            Activo = true,
            FechaInicio = DateTime.UtcNow.AddDays(-60),
            FechaFin = DateTime.UtcNow.AddDays(-30),
            EmpresaID = company.Id,
            Empresa = company,
        });
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetDashboardStatsRecord(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<DashboardStatsDto>>().Subject;
        ok.Value!.ActiveEmployeesCount.Should().Be(1);
        ok.Value.DepartmentsCount.Should().Be(1);
        ok.Value.ActiveCyclesCount.Should().Be(0);
        ok.Value.ActiveCycle.Should().BeNull();
        ok.Value.TotalPendingSubmissions.Should().Be(0);
        ok.Value.TotalCompletedSubmissions.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithActiveCycle_ReturnsProgressForIt()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var respondent1 = TestDataFactory.CreateUser(company, email: "r1@example.com");
        var respondent2 = TestDataFactory.CreateUser(company, email: "r2@example.com");
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(respondent1, respondent2, evaluated);

        var cycle = new EvaluationCycle
        {
            Nombre = "Ciclo activo",
            Activo = true,
            FechaInicio = DateTime.UtcNow.AddDays(-5),
            FechaFin = DateTime.UtcNow.AddDays(5),
            EmpresaID = company.Id,
            Empresa = company,
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        db.EvaluationSubmissions.AddRange(
            new EvaluationSubmission { Cycle = cycle, EvaluatedUser = evaluated, RespondentUser = respondent1, IsCompleted = true },
            new EvaluationSubmission { Cycle = cycle, EvaluatedUser = evaluated, RespondentUser = respondent2, IsCompleted = false }
        );
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetDashboardStatsRecord(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<DashboardStatsDto>>().Subject;
        ok.Value!.ActiveCyclesCount.Should().Be(1);
        ok.Value.ActiveCycle.Should().NotBeNull();
        ok.Value.ActiveCycle!.Id.Should().Be(cycle.Id);
        ok.Value.ActiveCycle.TotalSubmissions.Should().Be(2);
        ok.Value.ActiveCycle.CompletedCount.Should().Be(1);
        ok.Value.ActiveCycle.PendingCount.Should().Be(1);
        ok.Value.TotalPendingSubmissions.Should().Be(1);
        ok.Value.TotalCompletedSubmissions.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenARespondentHasMultipleSubmissions_CountsSubmissionsNotDistinctRespondents()
    {
        // Regression test: a 360° cycle with a manager + one report generates
        // 3 submissions — the manager's self-evaluation, the manager
        // evaluating the report, and the report's self-evaluation — but only
        // 2 distinct respondents (the manager appears twice). Progress must
        // be computed over the 3 submissions, or completing all of them
        // would previously read as 150% (3 completed / 2 "participants").
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var manager = TestDataFactory.CreateUser(company, email: "manager@example.com");
        var report = TestDataFactory.CreateUser(company, email: "report@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(manager, report);

        var cycle = new EvaluationCycle
        {
            Nombre = "Ciclo 360",
            Activo = true,
            FechaInicio = DateTime.UtcNow.AddDays(-5),
            FechaFin = DateTime.UtcNow.AddDays(5),
            EmpresaID = company.Id,
            Empresa = company,
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        db.EvaluationSubmissions.AddRange(
            new EvaluationSubmission { Cycle = cycle, EvaluatedUser = manager, RespondentUser = manager, IsCompleted = true },
            new EvaluationSubmission { Cycle = cycle, EvaluatedUser = report, RespondentUser = manager, IsCompleted = true },
            new EvaluationSubmission { Cycle = cycle, EvaluatedUser = report, RespondentUser = report, IsCompleted = true }
        );
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetDashboardStatsRecord(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<DashboardStatsDto>>().Subject;
        ok.Value!.ActiveCycle.Should().NotBeNull();
        ok.Value.ActiveCycle!.TotalSubmissions.Should().Be(3);
        ok.Value.ActiveCycle.CompletedCount.Should().Be(3);
        ok.Value.ActiveCycle.PendingCount.Should().Be(0);
    }
}
