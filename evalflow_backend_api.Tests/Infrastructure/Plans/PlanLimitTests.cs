using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Departments.CreateDepartment;
using evalflow_backend_api.Features.EvaluationCycles.CreateEvaluationCycle;
using evalflow_backend_api.Features.EvaluationCycles.UpdateEvaluationCycle;
using evalflow_backend_api.Features.Plans;
using evalflow_backend_api.Features.Team.InviteEmployee;
using evalflow_backend_api.Features.Team.ToggleUserStatus;
using evalflow_backend_api.Features.Templates.CreateTemplate;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Frontend;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Plans;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace evalflow_backend_api.Tests.Infrastructure.Plans;

/// <summary>Plan limits as enforced by the service and by every handler that applies them.</summary>
public class PlanLimitTests
{
    private const string Tenant = "tenant-plan";
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public PlanLimitTests()
    {
        _currentUser.Setup(c => c.GetIdentificationId()).Returns(Tenant);
    }

    private static async Task<Company> SeedCompanyAsync(AppDbContext db, int planId = PlanCatalog.Starter, int activeUsers = 0)
    {
        var company = TestDataFactory.CreateCompany(identificationId: Tenant, planId: planId);
        db.Companies.Add(company);
        for (var i = 0; i < activeUsers; i++) db.Users.Add(TestDataFactory.CreateUser(company));
        await db.SaveChangesAsync();
        return company;
    }

    private static EvaluationCycle Cycle(Company company, bool activo = false, int year = 2026) => new()
    {
        Nombre = "Ciclo",
        EmpresaID = company.Id,
        Activo = activo,
        FechaInicio = new DateTime(year, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        FechaFin = new DateTime(year, 4, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static void ShouldBePlanLimit(IResult result, string resource)
    {
        result.Should().HaveStatusCode(StatusCodes.Status403Forbidden);
        var error = result.Should().BeOfType<JsonHttpResult<PlanLimitError>>().Subject.Value!;
        error.Code.Should().Be(PlanLimitService.LimitReachedCode);
        error.Resource.Should().Be(resource);
    }

    [Fact]
    public async Task Service_FallsBackToCatalog_AndStarterForUnknownPlans()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = await SeedCompanyAsync(db, planId: 99);

        var plan = await new PlanLimitService(db).GetPlanAsync(company.Id, CancellationToken.None);

        plan.Id.Should().Be(PlanCatalog.Starter);
        plan.MaxEmployees.Should().Be(25);
    }

    [Theory]
    [InlineData(PlanCatalog.Starter, false)]
    [InlineData(PlanCatalog.Growth, true)]
    [InlineData(PlanCatalog.Enterprise, true)]
    public async Task Service_AiFeature_OnlyOnGrowthAndEnterprise(int planId, bool expected)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = await SeedCompanyAsync(db, planId);

        (await new PlanLimitService(db).HasFeatureAsync(company.Id, PlanFeature.Ai, CancellationToken.None))
            .Should().Be(expected);
    }

    [Fact]
    public async Task Service_Usage_CountsActiveUsersAndOnlyCustomTemplates()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = await SeedCompanyAsync(db, activeUsers: 3);
        db.Users.Add(TestDataFactory.CreateUser(company, activo: false));
        db.Templates.Add(new Template { Titulo = "Default", EmpresaID = company.Id, IsDefault = true });
        db.Templates.Add(new Template { Titulo = "Propia", EmpresaID = company.Id });
        await db.SaveChangesAsync();

        var usage = await new PlanLimitService(db).GetUsageAsync(company.Id, CancellationToken.None);

        usage.Employees.Should().Be(3);
        usage.CustomTemplates.Should().Be(1);
    }

    [Fact]
    public async Task Invite_AtEmployeeLimit_IsBlocked_AndBelowItPasses()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedCompanyAsync(db, activeUsers: 25);
        var sut = new InviteEmployeeHandler(db, _currentUser.Object, Mock.Of<IPasswordHasser>(h => h.Hash(It.IsAny<string>()) == "hash"), Mock.Of<IEmailService>(),
            Options.Create(new FrontendSettings { BaseUrl = "http://frontend.test" }), new PlanLimitService(db));

        var result = await sut.Handle(new InviteEmployeeRecord("Ana", "Pérez", "ana@example.com", AppRoles.Employee), CancellationToken.None);

        ShouldBePlanLimit(result, nameof(PlanLimit.Employees));
        (await db.Users.CountAsync()).Should().Be(25);
    }

    [Fact]
    public async Task Invite_OnEnterprise_IsNeverLimited()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedCompanyAsync(db, PlanCatalog.Enterprise, activeUsers: 120);
        var sut = new InviteEmployeeHandler(db, _currentUser.Object, Mock.Of<IPasswordHasser>(h => h.Hash(It.IsAny<string>()) == "hash"), Mock.Of<IEmailService>(),
            Options.Create(new FrontendSettings { BaseUrl = "http://frontend.test" }), new PlanLimitService(db));

        var result = await sut.Handle(new InviteEmployeeRecord("Ana", "Pérez", "ana@example.com", AppRoles.Employee), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task ToggleStatus_ReactivatingAtLimit_IsBlocked_ButDeactivatingIsAllowed()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = await SeedCompanyAsync(db, activeUsers: 25);
        var inactive = TestDataFactory.CreateUser(company, activo: false);
        db.Users.Add(inactive);
        await db.SaveChangesAsync();
        var anyActive = await db.Users.FirstAsync(u => u.Activo);
        var sut = new ToggleUserStatusHandler(db, _currentUser.Object, new PlanLimitService(db));

        ShouldBePlanLimit(await sut.Handle(new ToggleUserStatusRecord(inactive.Id, true), CancellationToken.None), nameof(PlanLimit.Employees));
        (await sut.Handle(new ToggleUserStatusRecord(anyActive.Id, false), CancellationToken.None))
            .Should().HaveStatusCode(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task CreateCycle_OverYearlyQuota_IsBlocked_ButAnotherYearPasses()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = await SeedCompanyAsync(db);
        for (var i = 0; i < 4; i++) db.EvaluationCycles.Add(Cycle(company, year: 2026));
        await db.SaveChangesAsync();
        var sut = new CreateEvaluationCycleHandler(db, _currentUser.Object, new PlanLimitService(db));

        var sameYear = await sut.Handle(new CreateEvaluationCycleRecord("Q4", null,
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc)), CancellationToken.None);
        var nextYear = await sut.Handle(new CreateEvaluationCycleRecord("Q1", null,
            new DateTime(2027, 1, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc)), CancellationToken.None);

        ShouldBePlanLimit(sameYear, nameof(PlanLimit.CyclesPerYear));
        nextYear.Should().HaveStatusCode(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task UpdateCycle_ActivatingOverActiveLimit_IsBlocked_ButEditingTheActiveOneIsNot()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = await SeedCompanyAsync(db);
        var active = Cycle(company, activo: true);
        var draft = Cycle(company);
        db.EvaluationCycles.AddRange(active, draft);
        await db.SaveChangesAsync();
        var sut = new UpdateEvaluationCycleHandler(db, _currentUser.Object, new PlanLimitService(db));

        var activate = await sut.Handle(new UpdateEvaluationCycleRecord(draft.Id, "Borrador", null, true, draft.FechaInicio, draft.FechaFin), CancellationToken.None);
        var rename = await sut.Handle(new UpdateEvaluationCycleRecord(active.Id, "Renombrado", null, true, active.FechaInicio, active.FechaFin), CancellationToken.None);

        ShouldBePlanLimit(activate, nameof(PlanLimit.ActiveCycles));
        rename.Should().HaveStatusCode(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task CreateTemplate_IgnoresDefaultTemplates_ButBlocksAtCustomLimit()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = await SeedCompanyAsync(db);
        db.Templates.Add(new Template { Titulo = "Default", EmpresaID = company.Id, IsDefault = true });
        for (var i = 0; i < 9; i++) db.Templates.Add(new Template { Titulo = $"T{i}", EmpresaID = company.Id });
        await db.SaveChangesAsync();
        var sut = new CreateTemplateHandler(db, _currentUser.Object, new PlanLimitService(db));
        var request = new CreateTemplateRecord("Nueva", null, DateTime.UtcNow, DateTime.UtcNow.AddDays(1), []);

        (await sut.Handle(request, CancellationToken.None)).Should().HaveStatusCode(StatusCodes.Status201Created); // 10th custom
        ShouldBePlanLimit(await sut.Handle(request, CancellationToken.None), nameof(PlanLimit.CustomTemplates));
    }

    [Fact]
    public async Task CreateDepartment_AtLimit_IsBlocked()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = await SeedCompanyAsync(db);
        for (var i = 0; i < 5; i++) db.Departments.Add(new Department { Nombre = $"D{i}", EmpresaID = company.Id });
        await db.SaveChangesAsync();

        var result = await new CreateDepartmentHandler(db, _currentUser.Object, new PlanLimitService(db))
            .Handle(new CreateDepartmentRecord("Otro", null), CancellationToken.None);

        ShouldBePlanLimit(result, nameof(PlanLimit.Departments));
    }

    [Fact]
    public async Task GetCompanyPlan_ReturnsPlanAndUsage()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedCompanyAsync(db, PlanCatalog.Growth, activeUsers: 7);

        var result = await new GetCompanyPlanHandler(db, _currentUser.Object, new PlanLimitService(db))
            .Handle(new GetCompanyPlanRecord(), CancellationToken.None);

        var dto = result.Should().BeOfType<Ok<CompanyPlanDto>>().Subject.Value!;
        dto.Plan.Nombre.Should().Be("Growth");
        dto.Plan.HasAiFeatures.Should().BeTrue();
        dto.Usage.Employees.Should().Be(7);
    }

    [Fact]
    public async Task ChangeCompanyPlan_UpdatesPlan_AndRejectsUnknownPlans()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = await SeedCompanyAsync(db);
        var sut = new ChangeCompanyPlanHandler(db);

        (await sut.Handle(new ChangeCompanyPlanRecord(Tenant, 42), CancellationToken.None))
            .Should().HaveStatusCode(StatusCodes.Status400BadRequest);
        (await sut.Handle(new ChangeCompanyPlanRecord(Tenant, PlanCatalog.Enterprise), CancellationToken.None))
            .Should().HaveStatusCode(StatusCodes.Status200OK);

        (await db.Companies.SingleAsync(c => c.Id == company.Id)).PlanId.Should().Be(PlanCatalog.Enterprise);
    }
}
