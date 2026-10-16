using System.Text.Json.Serialization;

namespace evalflow_backend_api.Features.AdminApi.Contract;

// The types of the Vexxen Admin API contract (/admin/v1, docs/admin-contract-v1.md in the vexxen-administrator repo)
// that EvalFlow publishes. JSON in camelCase (the minimal API defaults); optional properties are left out when null.

public static class AdminContract
{
    /// <summary>
    /// 1.1: the manifest declares actions (write) for callers that can run them. 1.2: and which fields of each resource
    /// can be edited (edit, PATCH /{resource}/{id}), to the same callers.
    /// </summary>
    public const string ContractVersion = "1.2";

    public const string ReadScope = "admin:read";
    public const string WriteScope = "admin:write";

    public const string ViewerRole = "viewer";
    public const string OperatorRole = "operator";
    public static readonly string[] Roles = [ViewerRole, OperatorRole];
}

public static class FieldTypes
{
    public const string String = "string";
    public const string Text = "text";
    public const string Number = "number";
    public const string Boolean = "boolean";
    public const string DateTime = "datetime";
    public const string Date = "date";
    public const string Enum = "enum";
    public const string Email = "email";
    public const string Url = "url";
    public const string Money = "money";
    public const string Json = "json";
    public const string Ref = "ref";
}

public static class FilterTypes
{
    public const string Enum = "enum";
    public const string Boolean = "boolean";
    public const string DateRange = "dateRange";
    public const string Ref = "ref";
}

/// <summary>An enum value. Tone: neutral, info, success, warning or danger.</summary>
public sealed record EnumOption(
    string Value,
    string Label,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Tone = null);

public sealed record RefTarget(string Resource);

public sealed record FieldDescriptor(string Key, string Label, string Type)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<EnumOption>? Enum { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RefTarget? Ref { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Nullable { get; init; }
}

public sealed record FilterDescriptor(string Key, string Label, string Type)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Multiple { get; init; }

    /// <summary>Enum filters: explicit options. Without them, the options of the field with the same key.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<EnumOption>? Options { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RefTarget? Ref { get; init; }
}

public sealed record SearchDescriptor(string Placeholder);

public sealed record ListDescriptor(
    IReadOnlyList<string> Columns,
    string DefaultSort,
    IReadOnlyList<string> Sortable,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SearchDescriptor? Search,
    IReadOnlyList<FilterDescriptor> Filters);

/// <summary>A link from the detail to another resource's list, filtered (Filter: a ref filter there) by this id.</summary>
public sealed record RelationDescriptor(string Label, string Resource, string Filter);

/// <summary>Default value of an action input: a fixed value or the current value of a field of the record.</summary>
public sealed record InputDefault
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Value { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FromField { get; init; }
}

/// <summary>An action input, or an editable field. Type: string, text, enum, boolean or number.</summary>
public sealed record ActionInputDescriptor(string Key, string Label, string Type, bool Required)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InputDefault? Default { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<EnumOption>? Enum { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxLength { get; init; }
}

/// <summary>The action is only offered when that field of the record has one of these values.</summary>
public sealed record ActionAppliesTo(string Field, IReadOnlyList<string> Values);

/// <summary>
/// POST /{resource}/{id}/actions/{key}. Confirm: the text shown before running it (always written, null when not
/// needed). Tone: default or danger.
/// </summary>
public sealed record ActionDescriptor(string Key, string Label, string? Description, string? Confirm, string Tone, IReadOnlyList<ActionInputDescriptor> Inputs)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActionAppliesTo? AppliesTo { get; init; }
}

/// <summary>
/// What PATCH /{resource}/{id} can change (contract §13.1). Each input's key is a field of the record (the desktop app
/// fills it with its current value). Confirm: the text shown before saving (always written, null when not needed).
/// </summary>
public sealed record EditDescriptor(string? Confirm, IReadOnlyList<ActionInputDescriptor> Inputs);

public sealed record ResourceDescriptor(
    string Key,
    string Label,
    string LabelSingular,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Group,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description,
    IReadOnlyList<FieldDescriptor> Fields,
    ListDescriptor List,
    bool Detail,
    IReadOnlyList<RelationDescriptor> Relations,
    IReadOnlyList<ActionDescriptor> Actions)
{
    /// <summary>Null (left out) when nothing can be edited, and for callers that can't write.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EditDescriptor? Edit { get; init; }
}

public sealed record AdminAppInfo(string Id, string Name, string Version, string Environment);

public sealed record AdminManifest(string ContractVersion, AdminAppInfo App, IReadOnlyList<ResourceDescriptor> Resources);

/// <summary>The value of a ref field: the app shows the label without one request per row.</summary>
public sealed record AdminRef(string Id, string Label);

public sealed record AdminMoney(long AmountCents, string Currency);

public sealed record AdminListResponse(IReadOnlyList<object> Items, int Page, int PageSize, int Total);

public sealed record AdminItemResponse(object Item);

/// <summary>Format: number, percent (0-100) or money (value in cents, with Currency).</summary>
public sealed record StatCard(string Key, string Label, double Value, string Format)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Currency { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Hint { get; init; }
}

public sealed record AdminStats(string GeneratedAt, IReadOnlyList<StatCard> Cards);

/// <summary>Status: ok, degraded or down.</summary>
public sealed record HealthCheck(
    string Name,
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? LatencyMs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message = null);

public sealed record AdminHealth(string Status, string Version, string Time, IReadOnlyList<HealthCheck> Checks);

/// <summary>Dates as the contract wants them: ISO 8601 in UTC (stored values are UTC; Unspecified is taken as UTC).</summary>
public static class AdminFormat
{
    public static string DateTime(System.DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => System.DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value,
        };
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string? DateTime(System.DateTime? value) => value is { } v ? DateTime(v) : null;
}
