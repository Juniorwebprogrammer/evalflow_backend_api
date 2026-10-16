using System.Net;
using System.Text;
using System.Text.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Admin.CreateAdministrator;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.AdminApi;

/// <summary>Record editing in the Admin API (contract §13): edit in the manifest and PATCH /{resource}/{id}.</summary>
public class AdminApiEditTests : IntegrationTestBase
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

    private static StringContent Changes(object changes) => new(JsonSerializer.Serialize(new { changes }), Encoding.UTF8, "application/json");

    private Task<HttpResponseMessage> Patch(HttpClient client, string path, object changes) =>
        client.PatchAsync($"/admin/v1/{path}", Changes(changes));

    private record Seeded(Company Acme, Company Internal, User Owner, User Hr, User Employee, User Administrator);

    /// <summary>Acme: its Owner pays the subscription (billing user), plus HR and an employee.</summary>
    private async Task<Seeded> SeedAsync()
    {
        var acme = TestDataFactory.CreateCompany(identificationId: "acme", nombre: "Acme", billingVexxenUserId: "vx-owner");
        acme.DireccionFiscal = "Calle Mayor 1";
        acme.Sector = "Retail";
        var internalCompany = TestDataFactory.CreateCompany(identificationId: CreateAdministratorCommand.InternalCompanyId, nombre: "evalflow");
        var owner = TestDataFactory.CreateUser(acme, email: "owner@acme.com", rol: AppRoles.Owner, vexxenUserId: "vx-owner");
        var hr = TestDataFactory.CreateUser(acme, email: "hr@acme.com", rol: AppRoles.Rrhh, vexxenUserId: "vx-hr");
        var employee = TestDataFactory.CreateUser(acme, email: "employee@acme.com", vexxenUserId: "vx-employee");
        var administrator = TestDataFactory.CreateUser(internalCompany, email: "admin@evalflow.com", rol: AppRoles.Administrator);

        await using var db = Factory.CreateDbContext();
        db.Companies.AddRange(acme, internalCompany);
        db.Users.AddRange(owner, hr, employee, administrator);
        await db.SaveChangesAsync();
        return new Seeded(acme, internalCompany, owner, hr, employee, administrator);
    }

    private async Task<User> UserAsync(int id)
    {
        await using var db = Factory.CreateDbContext();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
    }

    private async Task<Company> CompanyAsync(int id)
    {
        await using var db = Factory.CreateDbContext();
        return await db.Companies.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    // --- manifest ---

    private static JsonElement Resource(JsonElement manifest, string key) =>
        manifest.GetProperty("resources").EnumerateArray().Single(r => r.GetProperty("key").GetString() == key);

    [Fact]
    public async Task Manifest_ShowsEdit_ToOperators()
    {
        var manifest = await JsonAsync(await Operator.GetAsync("/admin/v1/manifest"));

        manifest.GetProperty("contractVersion").GetString().Should().Be("1.2");

        var companies = Resource(manifest, "companies").GetProperty("edit");
        companies.GetProperty("confirm").ValueKind.Should().Be(JsonValueKind.Null);
        var inputs = companies.GetProperty("inputs").EnumerateArray().ToList();
        inputs.Select(i => i.GetProperty("key").GetString()).Should().Equal("name", "cif", "fiscalAddress", "sector");
        inputs[0].GetProperty("type").GetString().Should().Be("string");
        inputs[0].GetProperty("required").GetBoolean().Should().BeTrue();
        inputs[0].GetProperty("maxLength").GetInt32().Should().Be(40);
        inputs[2].GetProperty("required").GetBoolean().Should().BeFalse();

        var users = Resource(manifest, "users").GetProperty("edit");
        users.GetProperty("confirm").GetString().Should().NotBeNullOrEmpty();
        var role = users.GetProperty("inputs").EnumerateArray().Single();
        role.GetProperty("key").GetString().Should().Be("role");
        role.GetProperty("type").GetString().Should().Be("enum");
        role.GetProperty("enum").EnumerateArray().Select(o => o.GetProperty("value").GetString())
            .Should().Equal("owner", "rrhh", "superior", "employee");

        // Every edit key is a field of the record (the desktop app fills it with the current value).
        foreach (var resource in manifest.GetProperty("resources").EnumerateArray().Where(r => r.TryGetProperty("edit", out _)))
        {
            var fields = resource.GetProperty("fields").EnumerateArray().Select(f => f.GetProperty("key").GetString()).ToHashSet();
            resource.GetProperty("edit").GetProperty("inputs").EnumerateArray().Should().OnlyContain(i => fields.Contains(i.GetProperty("key").GetString()));
        }

        Resource(manifest, "evaluation-cycles").TryGetProperty("edit", out _).Should().BeFalse();
        Resource(manifest, "email-outbox").TryGetProperty("edit", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Manifest_HidesEdit_FromViewers()
    {
        var manifest = await JsonAsync(await Viewer.GetAsync("/admin/v1/manifest"));

        manifest.GetProperty("contractVersion").GetString().Should().Be("1.2");
        manifest.GetProperty("resources").EnumerateArray().Where(r => r.TryGetProperty("edit", out _)).Should().BeEmpty();
    }

    [Fact]
    public async Task Manifest_HidesEdit_FromAnOperatorWithoutTheWriteScope()
    {
        var token = VexxenAdminTokens.Create(scope: "admin:read", grants: [VexxenAdminTokens.Grant("operator")]);

        var manifest = await JsonAsync(await AdminClient(token).GetAsync("/admin/v1/manifest"));

        manifest.GetProperty("resources").EnumerateArray().Where(r => r.TryGetProperty("edit", out _)).Should().BeEmpty();
    }

    // --- access ---

    [Fact]
    public async Task Patch_AsViewer_403_AndNothingChanges()
    {
        var seeded = await SeedAsync();

        var response = await Patch(Viewer, $"companies/{seeded.Acme.Id}", new { name = "Hacked" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await JsonAsync(response)).GetProperty("code").GetString().Should().Be("forbidden");
        (await CompanyAsync(seeded.Acme.Id)).Nombre.Should().Be("Acme");
    }

    [Fact]
    public async Task Patch_OperatorWithoutTheWriteScope_403()
    {
        var seeded = await SeedAsync();
        var token = VexxenAdminTokens.Create(scope: "admin:read", grants: [VexxenAdminTokens.Grant("operator")]);

        var response = await Patch(AdminClient(token), $"users/{seeded.Employee.Id}", new { role = "rrhh" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await UserAsync(seeded.Employee.Id)).Rol.Should().Be(AppRoles.Employee);
    }

    // --- companies ---

    [Fact]
    public async Task Patch_Company_ChangesOnlyTheFieldsGiven()
    {
        var seeded = await SeedAsync();

        var response = await Patch(Operator, $"companies/{seeded.Acme.Id}", new { name = "  Acme Iberia SL ", fiscalAddress = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var item = (await JsonAsync(response)).GetProperty("item");
        item.GetProperty("id").GetString().Should().Be(seeded.Acme.Id.ToString());
        item.GetProperty("name").GetString().Should().Be("Acme Iberia SL");
        item.GetProperty("fiscalAddress").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("sector").GetString().Should().Be("Retail");
        item.GetProperty("updatedAt").GetString().Should().NotBeNull();

        var stored = await CompanyAsync(seeded.Acme.Id);
        stored.Nombre.Should().Be("Acme Iberia SL");
        stored.DireccionFiscal.Should().BeNull();
        stored.Sector.Should().Be("Retail");
        stored.Cif.Should().Be(seeded.Acme.Cif);
        stored.IdentificationId.Should().Be("acme");
        stored.PlanId.Should().Be(seeded.Acme.PlanId);
    }

    [Fact]
    public async Task Patch_Company_EmptySector_IsStoredEmpty()
    {
        var seeded = await SeedAsync();

        var response = await Patch(Operator, $"companies/{seeded.Acme.Id}", new { sector = "", cif = "B87654321" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await CompanyAsync(seeded.Acme.Id);
        stored.Sector.Should().Be("");
        stored.Cif.Should().Be("B87654321");
    }

    [Fact]
    public async Task Patch_Company_InvalidChanges_400_WithEveryError_AndNothingChanges()
    {
        var seeded = await SeedAsync();

        var response = await Patch(Operator, $"companies/{seeded.Acme.Id}", new Dictionary<string, object?>
        {
            ["name"] = "",
            ["identificationId"] = "hijack",
            ["billingVexxenUserId"] = "vx-other",
            ["manualPlan"] = true,
            ["cif"] = new string('B', 16),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await JsonAsync(response);
        problem.GetProperty("code").GetString().Should().Be("invalid_input");
        problem.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString())
            .Should().BeEquivalentTo(["name", "identificationId", "billingVexxenUserId", "manualPlan", "cif"]);

        var stored = await CompanyAsync(seeded.Acme.Id);
        stored.Nombre.Should().Be("Acme");
        stored.IdentificationId.Should().Be("acme");
        stored.BillingVexxenUserId.Should().Be("vx-owner");
    }

    [Fact]
    public async Task Patch_MalformedOrEmptyBody_400()
    {
        var seeded = await SeedAsync();
        var url = $"/admin/v1/companies/{seeded.Acme.Id}";

        var malformed = await Operator.PatchAsync(url, new StringContent("{nope", Encoding.UTF8, "application/json"));
        var empty = await Operator.PatchAsync(url, new StringContent("", Encoding.UTF8, "application/json"));
        var nothing = await Operator.PatchAsync(url, Changes(new { }));

        foreach (var response in new[] { malformed, empty, nothing })
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await JsonAsync(response)).GetProperty("code").GetString().Should().Be("invalid_input");
        }
    }

    [Fact]
    public async Task Patch_UnknownOrInternalCompany_404()
    {
        var seeded = await SeedAsync();

        (await Patch(Operator, "companies/999999", new { name = "X" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Patch(Operator, "companies/abc", new { name = "X" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Patch(Operator, $"companies/{seeded.Internal.Id}", new { name = "X" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CompanyAsync(seeded.Internal.Id)).Nombre.Should().Be("evalflow");
    }

    [Theory]
    [InlineData("evaluation-cycles")]
    [InlineData("email-outbox")]
    [InlineData("templates")]
    public async Task Patch_AResourceWithoutEdit_404(string resource)
    {
        var response = await Patch(Operator, $"{resource}/1", new { name = "X" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await JsonAsync(response)).GetProperty("code").GetString().Should().Be("not_found");
    }

    // --- users ---

    [Fact]
    public async Task Patch_User_ChangesTheRole()
    {
        var seeded = await SeedAsync();

        var response = await Patch(Operator, $"users/{seeded.Employee.Id}", new { role = "superior" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await JsonAsync(response)).GetProperty("item");
        item.GetProperty("role").GetString().Should().Be("superior");
        item.GetProperty("email").GetString().Should().Be("employee@acme.com");
        (await UserAsync(seeded.Employee.Id)).Rol.Should().Be(AppRoles.Superior);
    }

    [Fact]
    public async Task Patch_User_PromoteToOwner_ThenTheFirstOwnerCanBeDemoted_IfTheyDontPay()
    {
        var seeded = await SeedAsync();
        // A second Owner who isn't the billing user.
        (await Patch(Operator, $"users/{seeded.Hr.Id}", new { role = "owner" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await Patch(Operator, $"users/{seeded.Hr.Id}", new { role = "rrhh" });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the billing Owner is still there");
        (await UserAsync(seeded.Hr.Id)).Rol.Should().Be(AppRoles.Rrhh);
    }

    [Fact]
    public async Task Patch_User_TheBillingOwner_CantBeDemoted_409()
    {
        var seeded = await SeedAsync();
        // Even with another Owner in the company: the one who pays stays Owner.
        (await Patch(Operator, $"users/{seeded.Hr.Id}", new { role = "owner" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await Patch(Operator, $"users/{seeded.Owner.Id}", new { role = "employee" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await JsonAsync(response)).GetProperty("code").GetString().Should().Be("conflict");
        (await UserAsync(seeded.Owner.Id)).Rol.Should().Be(AppRoles.Owner);
    }

    [Fact]
    public async Task Patch_User_TheLastActiveOwner_CantBeDemoted_409()
    {
        var seeded = await SeedAsync();
        await using (var db = Factory.CreateDbContext())
        {
            // A company without billing user (plan set by hand): only the "last Owner" rule applies.
            var acme = await db.Companies.SingleAsync(c => c.Id == seeded.Acme.Id);
            acme.BillingVexxenUserId = null;
            // A second Owner who is disabled and another one still invited don't count.
            var disabled = TestDataFactory.CreateUser(acme, email: "old-owner@acme.com", rol: AppRoles.Owner, activo: false);
            var invited = TestDataFactory.CreateUser(acme, email: "new-owner@acme.com", rol: AppRoles.Owner);
            invited.VexxenUserId = null;
            db.Users.AddRange(disabled, invited);
            await db.SaveChangesAsync();
        }

        var response = await Patch(Operator, $"users/{seeded.Owner.Id}", new { role = "rrhh" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await UserAsync(seeded.Owner.Id)).Rol.Should().Be(AppRoles.Owner);
    }

    [Fact]
    public async Task Patch_User_AnInvitedOwner_CanBeDemoted()
    {
        var seeded = await SeedAsync();
        int invitedId;
        await using (var db = Factory.CreateDbContext())
        {
            var invited = TestDataFactory.CreateUser(await db.Companies.SingleAsync(c => c.Id == seeded.Acme.Id), email: "new-owner@acme.com",
                rol: AppRoles.Owner);
            invited.VexxenUserId = null;
            db.Users.Add(invited);
            await db.SaveChangesAsync();
            invitedId = invited.Id;
        }

        (await Patch(Operator, $"users/{invitedId}", new { role = "employee" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await UserAsync(invitedId)).Rol.Should().Be(AppRoles.Employee);
    }

    [Fact]
    public async Task Patch_User_SameRole_IsANoOp()
    {
        var seeded = await SeedAsync();

        var response = await Patch(Operator, $"users/{seeded.Owner.Id}", new { role = "owner" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await UserAsync(seeded.Owner.Id)).Rol.Should().Be(AppRoles.Owner);
    }

    [Fact]
    public async Task Patch_User_NeverToAdministrator_400()
    {
        var seeded = await SeedAsync();

        var response = await Patch(Operator, $"users/{seeded.Employee.Id}", new { role = "administrator" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("role");
        (await UserAsync(seeded.Employee.Id)).Rol.Should().Be(AppRoles.Employee);
    }

    [Fact]
    public async Task Patch_User_AnAdministrator_IsNotFound()
    {
        var seeded = await SeedAsync();

        var response = await Patch(Operator, $"users/{seeded.Administrator.Id}", new { role = "employee" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await UserAsync(seeded.Administrator.Id)).Rol.Should().Be(AppRoles.Administrator);
    }

    [Fact]
    public async Task Patch_User_AnAdministratorOutsideTheInternalCompany_409()
    {
        var seeded = await SeedAsync();
        await using (var db = Factory.CreateDbContext())
        {
            var employee = await db.Users.SingleAsync(u => u.Id == seeded.Employee.Id);
            employee.Rol = AppRoles.Administrator;
            await db.SaveChangesAsync();
        }

        var response = await Patch(Operator, $"users/{seeded.Employee.Id}", new { role = "employee" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await UserAsync(seeded.Employee.Id)).Rol.Should().Be(AppRoles.Administrator);
    }

    [Fact]
    public async Task Patch_User_NameEmailAndCompany_AreNotEditable_400()
    {
        var seeded = await SeedAsync();

        var response = await Patch(Operator, $"users/{seeded.Employee.Id}", new { name = "Eve", email = "eve@evil.com", company = seeded.Internal.Id.ToString() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("errors").GetArrayLength().Should().Be(3);
        var stored = await UserAsync(seeded.Employee.Id);
        stored.Email.Should().Be("employee@acme.com");
        stored.EmpresaID.Should().Be(seeded.Acme.Id);
    }

    [Fact]
    public async Task Patch_UnknownUser_404()
    {
        await SeedAsync();

        (await Patch(Operator, "users/999999", new { role = "rrhh" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
