using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.Admin.CreateAdministrator;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.AdminApi;

/// <summary>Manifest, health, stats, lists, details and the set-plan action of the Admin API (contract §6 and §12).</summary>
public class AdminApiEndpointTests : IntegrationTestBase
{
    private HttpClient AdminClient(string token)
    {
        var client = CreateClient(includeApiKey: false);
        client.DefaultRequestHeaders.Authorization = VexxenAdminTokens.Bearer(token);
        return client;
    }

    private HttpClient Viewer => AdminClient(VexxenAdminTokens.Viewer());
    private HttpClient Operator => AdminClient(VexxenAdminTokens.Operator());

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private record Seeded(Company Acme, Company Globex, Company Internal, User AcmeOwner, User AcmeEmployee, User Administrator);

    private async Task<Seeded> SeedAsync()
    {
        var acme = TestDataFactory.CreateCompany(identificationId: "acme", nombre: "Acme", planId: PlanCatalog.Professional,
            billingVexxenUserId: "vx-acme-owner");
        acme.FechaCreacion = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var globex = TestDataFactory.CreateCompany(identificationId: "globex", nombre: "Globex", planId: PlanCatalog.Starter,
            planStatus: PlanStatuses.None);
        globex.FechaCreacion = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var internalCompany = TestDataFactory.CreateCompany(identificationId: CreateAdministratorCommand.InternalCompanyId, nombre: "evalflow",
            planId: PlanCatalog.Enterprise);

        var owner = TestDataFactory.CreateUser(acme, email: "owner@acme.com", rol: AppRoles.Owner, vexxenUserId: "vx-acme-owner");
        var employee = TestDataFactory.CreateUser(acme, email: "employee@acme.com");
        employee.VexxenUserId = null;
        employee.TokenInvitacion = "secret-invitation-token";
        employee.TokenInvitacionExpiracion = DateTime.UtcNow.AddDays(5);
        var globexUser = TestDataFactory.CreateUser(globex, email: "someone@globex.com", activo: false);
        var administrator = TestDataFactory.CreateUser(internalCompany, email: "admin@evalflow.com", rol: AppRoles.Administrator);

        var activeCycle = new EvaluationCycle
        {
            Nombre = "Q4", Empresa = acme, Activo = true,
            FechaInicio = DateTime.UtcNow.AddDays(-10), FechaFin = DateTime.UtcNow.AddDays(20),
        };
        var completedCycle = new EvaluationCycle
        {
            Nombre = "Q3", Empresa = acme, Activo = false, FechaCompletado = DateTime.UtcNow.AddDays(-30),
            FechaInicio = DateTime.UtcNow.AddDays(-100), FechaFin = DateTime.UtcNow.AddDays(-31),
        };
        var internalCycle = new EvaluationCycle
        {
            Nombre = "Internal", Empresa = internalCompany, Activo = true,
            FechaInicio = DateTime.UtcNow.AddDays(-1), FechaFin = DateTime.UtcNow.AddDays(1),
        };

        await using var db = Factory.CreateDbContext();
        db.Companies.AddRange(acme, globex, internalCompany);
        db.Users.AddRange(owner, employee, globexUser, administrator);
        db.EvaluationCycles.AddRange(activeCycle, completedCycle, internalCycle);
        await db.SaveChangesAsync();

        db.EvaluationSubmissions.AddRange(
            new EvaluationSubmission
            {
                EvaluationCycleId = activeCycle.Id, TemplateId = 1, EvaluatedUserId = owner.Id, RespondentUserId = owner.Id,
                IsCompleted = true, SubmittedAt = DateTime.UtcNow.AddDays(-2),
            },
            new EvaluationSubmission
            {
                EvaluationCycleId = completedCycle.Id, TemplateId = 1, EvaluatedUserId = owner.Id, RespondentUserId = owner.Id,
                IsCompleted = true, SubmittedAt = DateTime.UtcNow.AddDays(-60),
            },
            new EvaluationSubmission
            {
                EvaluationCycleId = activeCycle.Id, TemplateId = 1, EvaluatedUserId = employee.Id, RespondentUserId = employee.Id,
            },
            new EvaluationSubmission
            {
                EvaluationCycleId = internalCycle.Id, TemplateId = 1, EvaluatedUserId = administrator.Id, RespondentUserId = administrator.Id,
                IsCompleted = true, SubmittedAt = DateTime.UtcNow.AddDays(-1),
            });
        db.EmailOutboxMessages.AddRange(
            new EmailOutboxMessage { To = "a@acme.com", Subject = "Invitación", EncryptedHtmlBody = "ENCRYPTED", Status = EmailOutboxStatus.Fallido, Attempts = 5, LastError = "Brevo 500" },
            new EmailOutboxMessage { To = "b@acme.com", Subject = "Evaluación", EncryptedHtmlBody = "ENCRYPTED", Status = EmailOutboxStatus.Pendiente },
            new EmailOutboxMessage { To = "c@acme.com", Subject = "Resultados", EncryptedHtmlBody = "ENCRYPTED", Status = EmailOutboxStatus.Enviado, Attempts = 1 });
        await db.SaveChangesAsync();

        return new Seeded(acme, globex, internalCompany, owner, employee, administrator);
    }

    // --- manifest ---

    [Fact]
    public async Task Manifest_DescribesEvalFlow_AndItsResources()
    {
        var manifest = await JsonAsync(await Viewer.GetAsync("/admin/v1/manifest"));

        manifest.GetProperty("contractVersion").GetString().Should().Be("1.1");
        manifest.GetProperty("app").GetProperty("id").GetString().Should().Be("evalflow");
        manifest.GetProperty("app").GetProperty("name").GetString().Should().Be("EvalFlow");
        manifest.GetProperty("resources").EnumerateArray().Select(r => r.GetProperty("key").GetString())
            .Should().Equal("companies", "users", "evaluation-cycles", "email-outbox");

        var companies = manifest.GetProperty("resources")[0];
        companies.GetProperty("list").GetProperty("defaultSort").GetString().Should().Be("-createdAt");
        companies.GetProperty("relations").EnumerateArray().Select(r => r.GetProperty("resource").GetString())
            .Should().Equal("users", "evaluation-cycles");
        // Optional properties are left out, not sent as null.
        companies.GetProperty("fields")[0].TryGetProperty("enum", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Manifest_HidesTheActions_FromReadOnlyTokens()
    {
        var manifest = await JsonAsync(await Viewer.GetAsync("/admin/v1/manifest"));

        manifest.GetProperty("resources").EnumerateArray().Should().OnlyContain(r => r.GetProperty("actions").GetArrayLength() == 0);
    }

    [Fact]
    public async Task Manifest_HidesTheActions_FromAnOperatorWithoutTheWriteScope()
    {
        var token = VexxenAdminTokens.Create(scope: "admin:read", grants: [VexxenAdminTokens.Grant("operator")]);

        var manifest = await JsonAsync(await AdminClient(token).GetAsync("/admin/v1/manifest"));

        manifest.GetProperty("resources")[0].GetProperty("actions").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Manifest_ShowsSetPlan_ToOperators()
    {
        var manifest = await JsonAsync(await Operator.GetAsync("/admin/v1/manifest"));

        var action = manifest.GetProperty("resources")[0].GetProperty("actions").EnumerateArray().Single();
        action.GetProperty("key").GetString().Should().Be("set-plan");
        action.GetProperty("tone").GetString().Should().Be("default");
        action.TryGetProperty("confirm", out _).Should().BeTrue();
        var input = action.GetProperty("inputs").EnumerateArray().Single();
        input.GetProperty("key").GetString().Should().Be("planId");
        input.GetProperty("required").GetBoolean().Should().BeTrue();
        input.GetProperty("enum").EnumerateArray().Select(o => o.GetProperty("value").GetString())
            .Should().Equal("starter", "professional", "enterprise", "vexxen_sync");
    }

    // --- health and stats ---

    [Fact]
    public async Task Health_Ok_WithTheDatabaseCheck()
    {
        var response = await Viewer.GetAsync("/admin/v1/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var health = await JsonAsync(response);
        health.GetProperty("status").GetString().Should().Be("ok");
        health.GetProperty("time").GetString().Should().EndWith("Z");
        var check = health.GetProperty("checks").EnumerateArray().Single();
        check.GetProperty("name").GetString().Should().Be("database");
        check.GetProperty("status").GetString().Should().Be("ok");
        check.GetProperty("latencyMs").GetInt64().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Stats_LeaveTheInternalCompanyOut()
    {
        await SeedAsync();

        var stats = await JsonAsync(await Viewer.GetAsync("/admin/v1/stats"));

        var cards = stats.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("key").GetString()!, c => c.GetProperty("value").GetDouble());
        cards["companies_total"].Should().Be(2);
        cards["companies_plan_professional"].Should().Be(1);
        cards["companies_plan_starter"].Should().Be(0, "Globex has no plan");
        cards["companies_plan_enterprise"].Should().Be(0, "the internal company doesn't count");
        cards["companies_without_plan"].Should().Be(1);
        cards["users_total"].Should().Be(3);
        cards["cycles_active"].Should().Be(1);
        cards["evaluations_completed_30d"].Should().Be(1);
        cards["emails_failed"].Should().Be(1);
        cards["emails_pending"].Should().Be(1);
    }

    // --- lists and details ---

    [Fact]
    public async Task Companies_List_WithoutTheInternalCompany()
    {
        var seeded = await SeedAsync();

        var list = await JsonAsync(await Viewer.GetAsync("/admin/v1/companies"));

        list.GetProperty("total").GetInt32().Should().Be(2);
        list.GetProperty("page").GetInt32().Should().Be(1);
        list.GetProperty("pageSize").GetInt32().Should().Be(25);
        var items = list.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("name").GetString()).Should().Equal("Globex", "Acme");
        var acme = items[1];
        acme.GetProperty("id").GetString().Should().Be(seeded.Acme.Id.ToString());
        acme.GetProperty("identificationId").GetString().Should().Be("acme");
        acme.GetProperty("plan").GetString().Should().Be("professional");
        acme.GetProperty("planStatus").GetString().Should().Be("active");
        acme.GetProperty("manualPlan").GetBoolean().Should().BeFalse();
        acme.GetProperty("users").GetInt32().Should().Be(2);
        acme.GetProperty("createdAt").GetString().Should().Be("2026-09-01T00:00:00Z");
    }

    [Fact]
    public async Task Companies_FiltersSortSearchAndPages()
    {
        await SeedAsync();

        var none = await JsonAsync(await Viewer.GetAsync("/admin/v1/companies?planStatus=none"));
        none.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()).Should().Equal("Globex");

        var byName = await JsonAsync(await Viewer.GetAsync("/admin/v1/companies?sort=name&pageSize=1&page=2"));
        byName.GetProperty("total").GetInt32().Should().Be(2);
        byName.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()).Should().Equal("Globex");

        var search = await JsonAsync(await Viewer.GetAsync("/admin/v1/companies?q=ACM"));
        search.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()).Should().Equal("Acme");

        var since = await JsonAsync(await Viewer.GetAsync("/admin/v1/companies?createdAtFrom=2026-09-15&createdAtTo=2026-10-01"));
        since.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()).Should().Equal("Globex");
    }

    [Fact]
    public async Task List_UnknownFilter_400InvalidQuery()
    {
        var response = await Viewer.GetAsync("/admin/v1/companies?estado=active&sort=cif");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await JsonAsync(response);
        problem.GetProperty("code").GetString().Should().Be("invalid_query");
        problem.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()).Should().BeEquivalentTo(["estado", "sort"]);
    }

    [Fact]
    public async Task UnknownResource_404NotFound()
    {
        var response = await Viewer.GetAsync("/admin/v1/templates");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await JsonAsync(response)).GetProperty("code").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task Company_Detail_AndTheInternalOneIsNotFound()
    {
        var seeded = await SeedAsync();

        var detail = await JsonAsync(await Viewer.GetAsync($"/admin/v1/companies/{seeded.Acme.Id}"));
        detail.GetProperty("item").GetProperty("billingVexxenUserId").GetString().Should().Be("vx-acme-owner");
        detail.GetProperty("item").GetProperty("cif").GetString().Should().Be("B12345678");

        (await Viewer.GetAsync($"/admin/v1/companies/{seeded.Internal.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Viewer.GetAsync("/admin/v1/companies/abc")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Viewer.GetAsync("/admin/v1/companies/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Users_ByCompany_WithRefsAndStatus_WithoutSecrets()
    {
        var seeded = await SeedAsync();

        var response = await Viewer.GetAsync($"/admin/v1/users?company={seeded.Acme.Id}&sort=email");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("secret-invitation-token");
        var items = JsonDocument.Parse(body).RootElement.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("email").GetString()).Should().Equal("employee@acme.com", "owner@acme.com");
        items[0].GetProperty("status").GetString().Should().Be("invited");
        items[0].GetProperty("invitationExpiresAt").GetString().Should().NotBeNull();
        items[1].GetProperty("role").GetString().Should().Be("owner");
        items[1].GetProperty("status").GetString().Should().Be("active");
        items[1].GetProperty("company").GetProperty("id").GetString().Should().Be(seeded.Acme.Id.ToString());
        items[1].GetProperty("company").GetProperty("label").GetString().Should().Be("Acme");
    }

    [Fact]
    public async Task Users_LeaveTheAdministratorsOut_AndFilterByRoleAndStatus()
    {
        var seeded = await SeedAsync();

        var all = await JsonAsync(await Viewer.GetAsync("/admin/v1/users"));
        all.GetProperty("total").GetInt32().Should().Be(3);
        (await Viewer.GetAsync($"/admin/v1/users/{seeded.Administrator.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var owners = await JsonAsync(await Viewer.GetAsync("/admin/v1/users?role=owner"));
        owners.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("email").GetString()).Should().Equal("owner@acme.com");

        var inactive = await JsonAsync(await Viewer.GetAsync("/admin/v1/users?status=inactive"));
        inactive.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("email").GetString()).Should().Equal("someone@globex.com");

        var badRef = await Viewer.GetAsync("/admin/v1/users?company=acme");
        badRef.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EvaluationCycles_ByStatus()
    {
        var seeded = await SeedAsync();

        var active = await JsonAsync(await Viewer.GetAsync("/admin/v1/evaluation-cycles?status=active"));
        var item = active.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("name").GetString().Should().Be("Q4");
        item.GetProperty("company").GetProperty("label").GetString().Should().Be("Acme");

        var all = await JsonAsync(await Viewer.GetAsync($"/admin/v1/evaluation-cycles?company={seeded.Acme.Id}&status=active,completed"));
        all.GetProperty("total").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task EmailOutbox_FailedOnes_WithoutTheBody()
    {
        await SeedAsync();

        var response = await Viewer.GetAsync("/admin/v1/email-outbox?status=failed");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("ENCRYPTED");
        var item = JsonDocument.Parse(body).RootElement.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("to").GetString().Should().Be("a@acme.com");
        item.GetProperty("status").GetString().Should().Be("failed");
        item.GetProperty("attempts").GetInt32().Should().Be(5);
        item.GetProperty("lastError").GetString().Should().Be("Brevo 500");
    }

    // --- set-plan ---

    private static StringContent Inputs(object inputs) => new(JsonSerializer.Serialize(new { inputs }), Encoding.UTF8, "application/json");

    [Fact]
    public async Task SetPlan_AsViewer_403()
    {
        var seeded = await SeedAsync();

        var response = await Viewer.PostAsync($"/admin/v1/companies/{seeded.Acme.Id}/actions/set-plan", Inputs(new { planId = "enterprise" }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await JsonAsync(response)).GetProperty("code").GetString().Should().Be("forbidden");
    }

    [Fact]
    public async Task SetPlan_OperatorWithoutTheWriteScope_403()
    {
        var seeded = await SeedAsync();
        var token = VexxenAdminTokens.Create(scope: "admin:read", grants: [VexxenAdminTokens.Grant("operator")]);

        var response = await AdminClient(token).PostAsync($"/admin/v1/companies/{seeded.Acme.Id}/actions/set-plan", Inputs(new { planId = "enterprise" }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetPlan_InvalidInputs_400InvalidInput()
    {
        var seeded = await SeedAsync();
        var url = $"/admin/v1/companies/{seeded.Acme.Id}/actions/set-plan";

        var unknownPlan = await Operator.PostAsync(url, Inputs(new { planId = "gold" }));
        var missing = await Operator.PostAsync(url, Inputs(new { }));
        var malformed = await Operator.PostAsync(url, new StringContent("{not json", Encoding.UTF8, "application/json"));

        foreach (var response in new[] { unknownPlan, missing, malformed })
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var problem = await JsonAsync(response);
            problem.GetProperty("code").GetString().Should().Be("invalid_input");
            problem.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
        }
        await using var db = Factory.CreateDbContext();
        (await db.Companies.SingleAsync(c => c.Id == seeded.Acme.Id)).PlanManual.Should().BeFalse();
    }

    [Fact]
    public async Task SetPlan_UnknownActionOrCompany_404()
    {
        var seeded = await SeedAsync();

        (await Operator.PostAsync($"/admin/v1/companies/{seeded.Acme.Id}/actions/delete", Inputs(new { }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Operator.PostAsync("/admin/v1/companies/999999/actions/set-plan", Inputs(new { planId = "starter" }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Operator.PostAsync($"/admin/v1/companies/{seeded.Internal.Id}/actions/set-plan", Inputs(new { planId = "starter" })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Operator.PostAsync($"/admin/v1/users/{seeded.AcmeOwner.Id}/actions/set-plan", Inputs(new { planId = "starter" })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetPlan_Enterprise_ByHand()
    {
        var seeded = await SeedAsync();

        var response = await Operator.PostAsync($"/admin/v1/companies/{seeded.Globex.Id}/actions/set-plan", Inputs(new { planId = "enterprise" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var item = (await JsonAsync(response)).GetProperty("item");
        item.GetProperty("id").GetString().Should().Be(seeded.Globex.Id.ToString());
        item.GetProperty("plan").GetString().Should().Be("enterprise");
        item.GetProperty("planStatus").GetString().Should().Be("active");
        item.GetProperty("manualPlan").GetBoolean().Should().BeTrue();

        await using var db = Factory.CreateDbContext();
        var globex = await db.Companies.SingleAsync(c => c.Id == seeded.Globex.Id);
        globex.PlanId.Should().Be(PlanCatalog.Enterprise);
        globex.PlanManual.Should().BeTrue();
    }

    [Fact]
    public async Task SetPlan_BackToVexxen_ReadsTheSubscriptionAgain()
    {
        var seeded = await SeedAsync();
        Factory.FakeVexxen.SetPlan("vx-acme-owner", "starter");
        await Operator.PostAsync($"/admin/v1/companies/{seeded.Acme.Id}/actions/set-plan", Inputs(new { planId = "enterprise" }));

        var response = await Operator.PostAsync($"/admin/v1/companies/{seeded.Acme.Id}/actions/set-plan", Inputs(new { planId = "vexxen_sync" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await JsonAsync(response)).GetProperty("item");
        item.GetProperty("manualPlan").GetBoolean().Should().BeFalse();
        item.GetProperty("plan").GetString().Should().Be("starter");
    }

    [Fact]
    public async Task SetPlan_BackToVexxen_WithoutBillingUser_409Conflict()
    {
        var seeded = await SeedAsync();

        var response = await Operator.PostAsync($"/admin/v1/companies/{seeded.Globex.Id}/actions/set-plan", Inputs(new { planId = "vexxen_sync" }));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await JsonAsync(response)).GetProperty("code").GetString().Should().Be("conflict");
    }

    [Fact]
    public async Task SetPlan_DoesntNeedTheCompanyToHaveAPlan()
    {
        // Globex is read-only (no plan): PlanAccessMiddleware must not get in the way of the Admin API.
        var seeded = await SeedAsync();

        var response = await Operator.PostAsJsonAsync($"/admin/v1/companies/{seeded.Globex.Id}/actions/set-plan", new { inputs = new { planId = "starter" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
