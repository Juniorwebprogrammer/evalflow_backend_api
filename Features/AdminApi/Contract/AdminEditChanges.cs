using System.Text.Json;

namespace evalflow_backend_api.Features.AdminApi.Contract;

/// <summary>
/// The body of PATCH /{resource}/{id} (contract §13.2), { "changes": { … } }, checked against the resource's
/// edit.inputs: a field that isn't editable, a wrong type, an option that doesn't exist, a value that's too long or
/// emptying a required field is a 400 invalid_input with every problem in errors[]. Strings are trimmed; in an optional
/// field null or "" empty it.
/// </summary>
public static class AdminEditChanges
{
    /// <returns>Only the fields given: key → value (string, bool, decimal, or null to empty an optional field).</returns>
    public static IReadOnlyDictionary<string, object?> Validate(EditDescriptor edit, JsonElement? body)
    {
        if (body is not { ValueKind: JsonValueKind.Object } root)
            throw AdminApiException.InvalidInput("The body must be a JSON object: { \"changes\": { … } }", [new("changes", "Expected an object")]);
        if (!root.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Object)
            throw AdminApiException.InvalidInput("changes must be an object", [new("changes", "Expected an object")]);

        var errors = new List<AdminFieldError>();
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        var editable = edit.Inputs.ToDictionary(i => i.Key, StringComparer.Ordinal);

        foreach (var change in changes.EnumerateObject())
        {
            if (!editable.TryGetValue(change.Name, out var input))
            {
                errors.Add(new(change.Name, "Not editable"));
                continue;
            }

            var (value, error) = Parse(input, change.Value);
            if (error is not null) errors.Add(new(change.Name, error));
            else result[change.Name] = value;
        }

        if (errors.Count == 0 && result.Count == 0) errors.Add(new("changes", "Nothing to change"));
        if (errors.Count > 0) throw AdminApiException.InvalidInput($"Invalid changes: {errors[0].Field}", errors);
        return result;
    }

    private static (object? Value, string? Error) Parse(ActionInputDescriptor input, JsonElement value)
    {
        var empty = value.ValueKind == JsonValueKind.Null
                    || (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())
                        && input.Type is FieldTypes.String or FieldTypes.Text or FieldTypes.Enum);
        if (empty) return input.Required ? (null, "Required") : (null, null);

        if (input.Type is FieldTypes.String or FieldTypes.Text && value.ValueKind == JsonValueKind.String)
        {
            // Measured once trimmed: surrounding spaces are never stored.
            var text = value.GetString()!.Trim();
            return input.MaxLength is { } max && text.Length > max ? (null, $"At most {max} characters") : (text, null);
        }

        return AdminActionInputs.Parse(input, value);
    }
}
