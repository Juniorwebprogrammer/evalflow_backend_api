using System.Text.Json;

namespace evalflow_backend_api.Features.AdminApi.Contract;

/// <summary>
/// The body of POST /{resource}/{id}/actions/{action} (contract §12.3), { "inputs": { … } }, checked against the
/// action's inputs: a missing required input, an unknown one, a wrong type or a value that isn't an option is a
/// 400 invalid_input with every problem in errors[].
/// </summary>
public static class AdminActionInputs
{
    /// <returns>Input key → value (string, bool, decimal or null for an optional input left empty).</returns>
    public static IReadOnlyDictionary<string, object?> Validate(ActionDescriptor action, JsonElement? body)
    {
        var errors = new List<AdminFieldError>();
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);

        JsonElement? inputs = null;
        if (body is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } root)
        {
            if (root.ValueKind != JsonValueKind.Object)
                throw AdminApiException.InvalidInput("The body must be a JSON object: { \"inputs\": { … } }", [new("inputs", "Expected an object")]);

            if (root.TryGetProperty("inputs", out var given) && given.ValueKind != JsonValueKind.Null)
            {
                if (given.ValueKind != JsonValueKind.Object)
                    throw AdminApiException.InvalidInput("inputs must be an object", [new("inputs", "Expected an object")]);
                inputs = given;
            }
        }

        var declared = action.Inputs.ToDictionary(i => i.Key, StringComparer.Ordinal);
        if (inputs is { } provided)
        {
            foreach (var property in provided.EnumerateObject())
            {
                if (!declared.ContainsKey(property.Name)) errors.Add(new(property.Name, $"Unknown input for {action.Key}"));
            }
        }

        foreach (var input in action.Inputs)
        {
            JsonElement value = default;
            var present = inputs is { } obj && obj.TryGetProperty(input.Key, out value) && value.ValueKind != JsonValueKind.Null;
            if (!present)
            {
                if (input.Required) errors.Add(new(input.Key, "Required"));
                else result[input.Key] = null;
                continue;
            }

            var (parsed, error) = Parse(input, value);
            if (error is not null) errors.Add(new(input.Key, error));
            else result[input.Key] = parsed;
        }

        if (errors.Count > 0) throw AdminApiException.InvalidInput($"Invalid inputs for {action.Key}: {errors[0].Field}", errors);
        return result;
    }

    /// <summary>One value (not null) against its input: the value, or why it isn't valid. Shared with the edit changes.</summary>
    internal static (object? Value, string? Error) Parse(ActionInputDescriptor input, JsonElement value)
    {
        switch (input.Type)
        {
            case FieldTypes.String:
            case FieldTypes.Text:
            {
                if (value.ValueKind != JsonValueKind.String) return (null, "Expected a string");
                var text = value.GetString()!;
                if (input.Required && string.IsNullOrWhiteSpace(text)) return (null, "Required");
                if (input.MaxLength is { } max && text.Length > max) return (null, $"At most {max} characters");
                return (text, null);
            }
            case FieldTypes.Enum:
            {
                if (value.ValueKind != JsonValueKind.String) return (null, "Expected one of the options");
                var option = value.GetString()!;
                return input.Enum?.Any(o => o.Value == option) == true ? (option, null) : (null, $"Invalid option: {option}");
            }
            case FieldTypes.Boolean:
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? (value.GetBoolean(), null) : (null, "Expected true or false");
            case FieldTypes.Number:
                return value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? (number, null) : (null, "Expected a number");
            default:
                return (null, $"Unsupported input type {input.Type}");
        }
    }
}
