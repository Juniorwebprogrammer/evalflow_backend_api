using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Departments.CreateDepartment;
using evalflow_backend_api.Features.Profile.DeleteAccount;
using evalflow_backend_api.Infrastructure.Plans;
using evalflow_backend_api.Infrastructure.Vexxen;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace evalflow_backend_api.Tests.Infrastructure.Plans;

/// <summary>The company plan follows the Vexxen subscription of its billing user.</summary>
public class PlanSyncTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private static Company Company(int planId = PlanCatalog.Starter) =>
        TestDataFactory.CreateCompany(planId: planId, planStatus: PlanStatuses.Active, billingVexxenUserId: "vx-owner");

    [Theory]
    [InlineData("starter", "active", PlanCatalog.Starter, PlanStatuses.Active)]
    [InlineData("professional", "trialing", PlanCatalog.Professional, PlanStatuses.Trialing)]
    [InlineData("PROFESSIONAL", "canceling", PlanCatalog.Professional, PlanStatuses.Canceling)]
    [InlineData("enterprise", "past_due", PlanCatalog.Enterprise, PlanStatuses.PastDue)]
    public void Apply_CopiesThePlanAndItsStatus(string vexxenPlan, string status, int expectedPlan, string expectedStatus)
    {
        var company = Company();
        var trialEnds = Now.AddDays(10);

        PlanSync.Apply(company, FakeVexxenClient.Access(vexxenPlan, status, trialEndsAt: trialEnds), Now);

        company.PlanId.Should().Be(expectedPlan);
        company.PlanStatus.Should().Be(expectedStatus);
        company.TrialEndsAt.Should().Be(trialEnds);
        company.PlanSyncedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData("professional", "none")]
    [InlineData("professional", "free")]
    [InlineData("pro", "active")] // a plan EvalFlow doesn't sell
    [InlineData(null, "active")]
    public void Apply_WithoutAnEvalFlowPlan_LeavesTheCompanyReadOnly_KeepingItsPlanId(string? vexxenPlan, string status)
    {
        var company = Company(PlanCatalog.Professional);

        PlanSync.Apply(company, FakeVexxenClient.Access(vexxenPlan, status), Now);

        company.PlanStatus.Should().Be(PlanStatuses.None);
        company.PlanId.Should().Be(PlanCatalog.Professional);
        PlanStatuses.HasAccess(company.PlanStatus).Should().BeFalse();
    }

    private static async Task<(PlanSyncService Sut, FakeVexxenClient Vexxen, Company Company, evalflow_backend_api.Infrastructure.Database.AppDbContext Db)> SetupAsync(
        Action<Company>? configure = null)
    {
        var db = InMemoryDbContextFactory.Create();
        var company = Company();
        configure?.Invoke(company);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var vexxen = new FakeVexxenClient();
        return (new PlanSyncService(db, vexxen, NullLogger<PlanSyncService>.Instance), vexxen, company, db);
    }

    [Fact]
    public async Task Sync_SavesWhatVexxenSays()
    {
        var (sut, vexxen, company, db) = await SetupAsync();
        vexxen.SetPlan("vx-owner", "professional", "trialing");

        await sut.SyncAsync(company, CancellationToken.None);

        var saved = await db.Companies.AsNoTracking().SingleAsync();
        saved.PlanId.Should().Be(PlanCatalog.Professional);
        saved.PlanStatus.Should().Be(PlanStatuses.Trialing);
    }

    [Fact]
    public async Task Sync_LeavesAManualPlanAlone()
    {
        var (sut, vexxen, company, _) = await SetupAsync(c =>
        {
            c.PlanId = PlanCatalog.Enterprise;
            c.PlanManual = true;
        });

        await sut.SyncAsync(company, CancellationToken.None, force: true);

        vexxen.AccessCalls.Should().Be(0);
        company.PlanId.Should().Be(PlanCatalog.Enterprise);
        company.PlanStatus.Should().Be(PlanStatuses.Active);
    }

    [Fact]
    public async Task Sync_ReadRecently_IsSkippedUnlessForced()
    {
        var (sut, vexxen, company, _) = await SetupAsync(c => c.PlanSyncedAt = DateTime.UtcNow.AddMinutes(-1));
        vexxen.SetPlan("vx-owner", "professional");

        await sut.SyncAsync(company, CancellationToken.None);
        vexxen.AccessCalls.Should().Be(0);

        await sut.SyncAsync(company, CancellationToken.None, force: true);
        vexxen.AccessCalls.Should().Be(1);
        company.PlanId.Should().Be(PlanCatalog.Professional);
    }

    [Fact]
    public async Task Sync_WhenVexxenIsDown_KeepsTheLastKnownPlan()
    {
        var (sut, vexxen, company, db) = await SetupAsync();
        vexxen.Down = true;

        await sut.SyncAsync(company, CancellationToken.None);

        var saved = await db.Companies.AsNoTracking().SingleAsync();
        saved.PlanStatus.Should().Be(PlanStatuses.Active);
        saved.PlanSyncedAt.Should().BeNull();
    }

    [Fact]
    public async Task Sync_WhenTheBillingAccountWasDeleted_LeavesTheCompanyWithoutPlan()
    {
        var (sut, vexxen, company, db) = await SetupAsync();
        vexxen.DeletedUserIds.Add("vx-owner");

        await sut.SyncAsync(company, CancellationToken.None);

        (await db.Companies.AsNoTracking().SingleAsync()).PlanStatus.Should().Be(PlanStatuses.None);
    }
}

/// <summary>Without a plan the company is read-only: writes answer 403 PLAN_INACTIVE.</summary>
public class PlanAccessMiddlewareTests : IntegrationTestBase
{
    private async Task<HttpClient> MemberOfCompanyAsync(string planStatus, string rol = AppRoles.Owner)
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1", planStatus: planStatus);
        var user = TestDataFactory.CreateUser(company, email: "ana@acme.com", rol: rol);
        await using var db = Factory.CreateDbContext();
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return CreateAuthenticatedClient(user, company);
    }

    [Theory]
    [InlineData(PlanStatuses.None)]
    public async Task WithoutPlan_WritesAreBlocked(string status)
    {
        var client = await MemberOfCompanyAsync(status);

        var response = await client.PostAsJsonAsync("/departments", new CreateDepartmentRecord("Ventas", null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain(PlanAccessMiddleware.PlanInactiveCode);
        await using var db = Factory.CreateDbContext();
        (await db.Departments.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task WithoutPlan_ReadsStillWork()
    {
        var client = await MemberOfCompanyAsync(PlanStatuses.None);

        (await client.GetAsync("/departments")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/Profile/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithoutPlan_TheProfileStaysWritable()
    {
        var client = await MemberOfCompanyAsync(PlanStatuses.None, AppRoles.Employee);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/Profile/delete")
        {
            Content = JsonContent.Create(new DeleteAccountRecord("ana@acme.com")),
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(PlanStatuses.Trialing)]
    [InlineData(PlanStatuses.Active)]
    [InlineData(PlanStatuses.Canceling)]
    [InlineData(PlanStatuses.PastDue)]
    public async Task WithAPlan_WritesWork(string status)
    {
        var client = await MemberOfCompanyAsync(status);

        var response = await client.PostAsJsonAsync("/departments", new CreateDepartmentRecord("Ventas", null));

        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
