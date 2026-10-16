using evalflow_backend_api.Features.AdminApi.Contract;
using evalflow_backend_api.Features.AdminApi.Resources;

namespace evalflow_backend_api.Tests.Features.AdminApi;

/// <summary>The list query (contract §6.5): whatever the resource doesn't declare is a 400 invalid_query, never ignored.</summary>
public class AdminListQueryTests
{
    private static readonly ResourceDescriptor Companies = CompaniesAdminResource.Definition;

    private static AdminListQuery Parse(params (string Key, string Value)[] parameters) =>
        AdminListQuery.Parse(Companies, parameters.Select(p => new KeyValuePair<string, string>(p.Key, p.Value)));

    private static AdminApiException Invalid(params (string Key, string Value)[] parameters)
    {
        var act = () => Parse(parameters);
        var error = act.Should().Throw<AdminApiException>().Which;
        error.Status.Should().Be(400);
        error.Code.Should().Be("invalid_query");
        return error;
    }

    [Fact]
    public void Defaults_Page1_25PerPage_TheDefaultSort()
    {
        var query = Parse();

        query.Page.Should().Be(1);
        query.PageSize.Should().Be(25);
        query.Q.Should().BeNull();
        query.Sort.Should().Be(new AdminSort("createdAt", SortDirection.Desc));
        query.Filters.Should().BeEmpty();
    }

    [Fact]
    public void PageSize_AboveTheMaximum_IsCappedAt100()
    {
        Parse(("pageSize", "500")).PageSize.Should().Be(100);
    }

    [Theory]
    [InlineData("page", "0")]
    [InlineData("page", "-1")]
    [InlineData("page", "abc")]
    [InlineData("pageSize", "1.5")]
    public void NotAPositiveInteger_IsInvalid(string key, string value)
    {
        Invalid((key, value)).Errors.Should().ContainSingle(e => e.Field == key);
    }

    [Fact]
    public void UnknownFilter_IsInvalid()
    {
        var error = Invalid(("estado", "open"));

        error.Errors.Should().ContainSingle(e => e.Field == "estado" && e.Message.Contains("Unknown filter"));
    }

    [Fact]
    public void SortByAFieldThatIsNotSortable_IsInvalid()
    {
        Invalid(("sort", "-cif")).Errors.Should().ContainSingle(e => e.Field == "sort");
    }

    [Fact]
    public void Sort_AscendingAndDescending()
    {
        Parse(("sort", "name")).Sort.Should().Be(new AdminSort("name", SortDirection.Asc));
        Parse(("sort", "-users")).Sort.Should().Be(new AdminSort("users", SortDirection.Desc));
    }

    [Fact]
    public void ReservedParameterRepeated_IsInvalid()
    {
        Invalid(("page", "1"), ("page", "2")).Errors.Should().ContainSingle(e => e.Field == "page");
    }

    [Fact]
    public void Search_IsTrimmed_AndEmptyMeansNone()
    {
        Parse(("q", "  acme ")).Q.Should().Be("acme");
        Parse(("q", "   ")).Q.Should().BeNull();
    }

    [Fact]
    public void Search_OnAResourceWithoutSearch_IsInvalid()
    {
        var withoutSearch = Companies with { List = Companies.List with { Search = null } };

        var act = () => AdminListQuery.Parse(withoutSearch, [new("q", "acme")]);

        act.Should().Throw<AdminApiException>().Which.Errors.Should().ContainSingle(e => e.Field == "q");
    }

    [Fact]
    public void EnumFilter_Multiple_RepeatedOrCommaSeparated()
    {
        var query = Parse(("planStatus", "active"), ("planStatus", "trialing,none"));

        query.Filter<EnumFilterValue>("planStatus")!.Values.Should().BeEquivalentTo(["active", "trialing", "none"]);
    }

    [Fact]
    public void EnumFilter_ValueThatIsNotAnOption_IsInvalid()
    {
        Invalid(("plan", "gold")).Errors.Should().ContainSingle(e => e.Field == "plan" && e.Message.Contains("gold"));
    }

    [Fact]
    public void EnumFilter_NotMultiple_RejectsSeveralValues()
    {
        var single = Companies with
        {
            List = Companies.List with { Filters = [new FilterDescriptor("plan", "Plan", FilterTypes.Enum)] },
        };

        var act = () => AdminListQuery.Parse(single, [new("plan", "starter"), new("plan", "enterprise")]);

        act.Should().Throw<AdminApiException>().Which.Errors.Should().ContainSingle(e => e.Field == "plan");
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void BooleanFilter(string raw, bool expected)
    {
        Parse(("manualPlan", raw)).Filter<BooleanFilterValue>("manualPlan")!.Value.Should().Be(expected);
    }

    [Fact]
    public void BooleanFilter_AnythingElse_IsInvalid()
    {
        Invalid(("manualPlan", "yes")).Errors.Should().ContainSingle(e => e.Field == "manualPlan");
    }

    [Fact]
    public void DateRange_ADateCoversTheWholeDay()
    {
        var range = Parse(("createdAtFrom", "2026-10-01"), ("createdAtTo", "2026-10-16")).Filter<DateRangeFilterValue>("createdAt")!;

        range.From.Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        range.To.Should().BeAfter(new DateTime(2026, 10, 16, 23, 59, 59, DateTimeKind.Utc));
        range.To.Should().BeBefore(new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void DateRange_AFullIsoDate_AndOnlyOneEnd()
    {
        var range = Parse(("createdAtFrom", "2026-10-01T10:30:00Z")).Filter<DateRangeFilterValue>("createdAt")!;

        range.From.Should().Be(new DateTime(2026, 10, 1, 10, 30, 0, DateTimeKind.Utc));
        range.To.Should().BeNull();
    }

    [Theory]
    [InlineData("yesterday")]
    [InlineData("2026-13-45")]
    [InlineData("16/10/2026")]
    public void DateRange_NotAnIsoDate_IsInvalid(string raw)
    {
        Invalid(("createdAtTo", raw)).Errors.Should().ContainSingle(e => e.Field == "createdAtTo");
    }

    [Fact]
    public void RefFilter_TakesOneId()
    {
        var users = UsersAdminResource.Definition;

        AdminListQuery.Parse(users, [new("company", "42")]).Filter<RefFilterValue>("company")!.Value.Should().Be("42");

        var repeated = () => AdminListQuery.Parse(users, [new("company", "1"), new("company", "2")]);
        repeated.Should().Throw<AdminApiException>().Which.Errors.Should().ContainSingle(e => e.Field == "company");
    }

    [Fact]
    public void EveryError_IsReported()
    {
        var error = Invalid(("page", "0"), ("sort", "cif"), ("foo", "bar"));

        error.Errors.Select(e => e.Field).Should().BeEquivalentTo(["page", "sort", "foo"]);
    }
}
