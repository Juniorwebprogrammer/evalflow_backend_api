using System.Text.Json;
using evalflow_backend_api.Features.AdminApi.Contract;
using evalflow_backend_api.Features.AdminApi.Resources;

namespace evalflow_backend_api.Tests.Features.AdminApi;

/// <summary>The inputs of an action (contract §12.3): missing, unknown, wrong type or not an option → 400 invalid_input.</summary>
public class AdminActionInputsTests
{
    private static readonly ActionDescriptor SetPlan = CompaniesAdminResource.Definition.Actions.Single(a => a.Key == "set-plan");

    private static readonly ActionDescriptor Note = new("note", "Nota", null, null, "default",
    [
        new ActionInputDescriptor("text", "Texto", FieldTypes.Text, true) { MaxLength = 5 },
        new ActionInputDescriptor("urgent", "Urgente", FieldTypes.Boolean, false),
        new ActionInputDescriptor("priority", "Prioridad", FieldTypes.Number, false),
    ]);

    private static JsonElement Body(string json) => JsonDocument.Parse(json).RootElement;

    private static AdminApiException Invalid(ActionDescriptor action, string? json)
    {
        var act = () => AdminActionInputs.Validate(action, json is null ? null : Body(json));
        var error = act.Should().Throw<AdminApiException>().Which;
        error.Status.Should().Be(400);
        error.Code.Should().Be("invalid_input");
        return error;
    }

    [Theory]
    [InlineData("starter")]
    [InlineData("professional")]
    [InlineData("enterprise")]
    [InlineData("vexxen_sync")]
    public void SetPlan_AcceptsEveryPlan_AndBackToVexxen(string planId)
    {
        var inputs = AdminActionInputs.Validate(SetPlan, Body(JsonSerializer.Serialize(new { inputs = new { planId } })));

        inputs["planId"].Should().Be(planId);
    }

    [Fact]
    public void SetPlan_PlanIsRequired()
    {
        Invalid(SetPlan, """{"inputs":{}}""").Errors.Should().ContainSingle(e => e.Field == "planId" && e.Message == "Required");
        Invalid(SetPlan, null).Errors.Should().ContainSingle(e => e.Field == "planId");
        Invalid(SetPlan, """{"inputs":{"planId":null}}""").Errors.Should().ContainSingle(e => e.Field == "planId");
    }

    [Fact]
    public void SetPlan_APlanThatDoesntExist_IsInvalid()
    {
        Invalid(SetPlan, """{"inputs":{"planId":"gold"}}""").Errors.Should().ContainSingle(e => e.Field == "planId");
    }

    [Fact]
    public void SetPlan_TheNumericId_IsNotAnOption()
    {
        Invalid(SetPlan, """{"inputs":{"planId":3}}""").Errors.Should().ContainSingle(e => e.Field == "planId");
    }

    [Fact]
    public void UnknownInputs_AreRejected()
    {
        var error = Invalid(SetPlan, """{"inputs":{"planId":"starter","force":true}}""");

        error.Errors.Should().ContainSingle(e => e.Field == "force");
    }

    [Fact]
    public void InputsMustBeAnObject()
    {
        Invalid(SetPlan, """{"inputs":["starter"]}""").Errors.Should().ContainSingle(e => e.Field == "inputs");
        Invalid(SetPlan, "[1]").Errors.Should().ContainSingle(e => e.Field == "inputs");
    }

    [Fact]
    public void Types_AndMaxLength_AreChecked()
    {
        var error = Invalid(Note, """{"inputs":{"text":"too long","urgent":"yes","priority":"high"}}""");

        error.Errors.Select(e => e.Field).Should().BeEquivalentTo(["text", "urgent", "priority"]);
    }

    [Fact]
    public void OptionalInputs_CanBeLeftOut()
    {
        var inputs = AdminActionInputs.Validate(Note, Body("""{"inputs":{"text":"hola","priority":2}}"""));

        inputs["text"].Should().Be("hola");
        inputs["urgent"].Should().BeNull();
        inputs["priority"].Should().Be(2m);
    }
}
