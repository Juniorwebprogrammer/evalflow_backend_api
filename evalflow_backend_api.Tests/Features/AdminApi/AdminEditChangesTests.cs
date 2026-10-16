using System.Text.Json;
using evalflow_backend_api.Features.AdminApi.Contract;
using evalflow_backend_api.Features.AdminApi.Resources;

namespace evalflow_backend_api.Tests.Features.AdminApi;

/// <summary>
/// The changes of PATCH /{resource}/{id} (contract §13.2): a field that isn't editable, a wrong type, an option that
/// doesn't exist, a value that's too long or emptying a required field → 400 invalid_input.
/// </summary>
public class AdminEditChangesTests
{
    private static readonly EditDescriptor Companies = CompaniesAdminResource.Definition.Edit!;
    private static readonly EditDescriptor Users = UsersAdminResource.Definition.Edit!;

    private static JsonElement Body(object changes) => JsonDocument.Parse(JsonSerializer.Serialize(new { changes })).RootElement;

    private static AdminApiException Invalid(EditDescriptor edit, JsonElement? body)
    {
        var act = () => AdminEditChanges.Validate(edit, body);
        var error = act.Should().Throw<AdminApiException>().Which;
        error.Status.Should().Be(400);
        error.Code.Should().Be("invalid_input");
        return error;
    }

    [Fact]
    public void OnlyTheFieldsGiven_Trimmed()
    {
        var changes = AdminEditChanges.Validate(Companies, Body(new { name = "  Acme SL  " }));

        changes.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, object?>("name", "Acme SL"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnOptionalField_IsEmptiedWithNullOrEmpty(string? value)
    {
        var changes = AdminEditChanges.Validate(Companies, Body(new Dictionary<string, object?> { ["fiscalAddress"] = value }));

        changes["fiscalAddress"].Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ARequiredField_CantBeEmptied(string? value)
    {
        var error = Invalid(Companies, Body(new Dictionary<string, object?> { ["name"] = value }));

        error.Errors.Should().ContainSingle().Which.Should().Be(new AdminFieldError("name", "Required"));
    }

    [Fact]
    public void EveryProblem_IsInErrors()
    {
        var error = Invalid(Companies, Body(new Dictionary<string, object?>
        {
            ["identificationId"] = "other",      // not editable
            ["plan"] = "enterprise",             // not editable (set-plan)
            ["cif"] = 12345678,                  // wrong type
            ["sector"] = new string('x', 51),    // too long
        }));

        error.Errors.Select(e => e.Field).Should().BeEquivalentTo(["identificationId", "plan", "cif", "sector"]);
        error.Errors.Single(e => e.Field == "identificationId").Message.Should().Be("Not editable");
        error.Errors.Single(e => e.Field == "cif").Message.Should().Be("Expected a string");
        error.Errors.Single(e => e.Field == "sector").Message.Should().Be("At most 50 characters");
    }

    [Fact]
    public void TheLength_IsMeasuredTrimmed()
    {
        var changes = AdminEditChanges.Validate(Companies, Body(new { name = $"  {new string('a', 40)}  " }));

        changes["name"].Should().Be(new string('a', 40));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("rrhh")]
    [InlineData("superior")]
    [InlineData("employee")]
    public void Role_AcceptsTheCompanyRoles(string role)
    {
        AdminEditChanges.Validate(Users, Body(new { role }))["role"].Should().Be(role);
    }

    [Theory]
    [InlineData("administrator")]
    [InlineData("Administrator")]
    [InlineData("Owner")]
    [InlineData("boss")]
    public void Role_RejectsAnythingElse_AdministratorIncluded(string role)
    {
        var error = Invalid(Users, Body(new { role }));

        error.Errors.Should().ContainSingle().Which.Field.Should().Be("role");
    }

    [Fact]
    public void Role_IsRequired()
    {
        Invalid(Users, Body(new Dictionary<string, object?> { ["role"] = null }));
    }

    [Fact]
    public void Users_NameAndEmailAndCompany_AreNotEditable()
    {
        var error = Invalid(Users, Body(new { name = "Ana", email = "ana@acme.com", company = "2", firstName = "Ana" }));

        error.Errors.Should().OnlyContain(e => e.Message == "Not editable").And.HaveCount(4);
    }

    [Fact]
    public void NoChanges_400()
    {
        Invalid(Companies, Body(new { })).Errors.Single().Field.Should().Be("changes");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"changes":"name"}""")]
    [InlineData("""{"inputs":{"name":"Acme"}}""")]
    public void ABodyWithoutAChangesObject_400(string? json)
    {
        Invalid(Companies, json is null ? null : JsonDocument.Parse(json).RootElement).Errors.Single().Field.Should().Be("changes");
    }
}
