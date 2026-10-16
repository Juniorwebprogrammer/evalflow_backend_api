using System.Globalization;
using System.Text.RegularExpressions;

namespace evalflow_backend_api.Features.AdminApi.Contract;

public enum SortDirection
{
    Asc,
    Desc,
}

public sealed record AdminSort(string Key, SortDirection Direction)
{
    public bool Descending => Direction == SortDirection.Desc;

    public static AdminSort Parse(string raw) =>
        raw.StartsWith('-') ? new(raw[1..], SortDirection.Desc) : new(raw, SortDirection.Asc);
}

public abstract record AdminFilterValue(string Key);

public sealed record EnumFilterValue(string Key, IReadOnlyList<string> Values) : AdminFilterValue(Key);

public sealed record BooleanFilterValue(string Key, bool Value) : AdminFilterValue(Key);

/// <summary>Both ends inclusive. A date without time covers the whole day (UTC).</summary>
public sealed record DateRangeFilterValue(string Key, DateTime? From, DateTime? To) : AdminFilterValue(Key);

public sealed record RefFilterValue(string Key, string Value) : AdminFilterValue(Key);

/// <summary>
/// The query of GET /{resource} (contract §6.5), validated against the resource descriptor: page, pageSize, q, sort
/// and the filters. Whatever the resource doesn't declare is rejected (400 invalid_query), never silently ignored.
/// Port of vexxen-home's features/admin-api/domain/list-query.ts.
/// </summary>
public sealed partial record AdminListQuery(int Page, int PageSize, string? Q, AdminSort Sort, IReadOnlyList<AdminFilterValue> Filters)
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    private const int MaxSearchLength = 200;
    private static readonly HashSet<string> Reserved = ["page", "pageSize", "q", "sort"];

    public T? Filter<T>(string key) where T : AdminFilterValue => Filters.OfType<T>().FirstOrDefault(f => f.Key == key);

    /// <summary>How many rows to skip. Null when the page is so far away that it can only be empty.</summary>
    public int? Skip
    {
        get
        {
            var skip = (long)(Page - 1) * PageSize;
            return skip > int.MaxValue ? null : (int)skip;
        }
    }

    /// <param name="parameters">The URL parameters as they come (a key can repeat).</param>
    public static AdminListQuery Parse(ResourceDescriptor resource, IEnumerable<KeyValuePair<string, string>> parameters)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (key, value) in parameters)
        {
            if (!values.TryGetValue(key, out var list)) values[key] = list = [];
            list.Add(value);
        }

        var errors = new List<AdminFieldError>();
        string? Single(string key)
        {
            if (!values.TryGetValue(key, out var list)) return null;
            if (list.Count > 1) errors.Add(new(key, "Only one value allowed"));
            return list[0];
        }

        var page = ParsePositiveInt(Single("page"), 1, "page", errors);
        var pageSize = Math.Min(ParsePositiveInt(Single("pageSize"), DefaultPageSize, "pageSize", errors), MaxPageSize);

        var rawQ = Single("q")?.Trim();
        if (string.IsNullOrEmpty(rawQ)) rawQ = null;
        if (rawQ is not null && resource.List.Search is null) errors.Add(new("q", $"{resource.Key} doesn't support search"));
        var q = rawQ is null ? null : rawQ[..Math.Min(rawQ.Length, MaxSearchLength)];

        var sort = ParseSort(Single("sort") ?? resource.List.DefaultSort, resource, errors);

        // Which parameters each filter takes: its key or, date ranges, {key}From and {key}To.
        var byParam = new Dictionary<string, FilterDescriptor>(StringComparer.Ordinal);
        foreach (var filter in resource.List.Filters)
        {
            if (filter.Type == FilterTypes.DateRange)
            {
                byParam[$"{filter.Key}From"] = filter;
                byParam[$"{filter.Key}To"] = filter;
            }
            else
            {
                byParam[filter.Key] = filter;
            }
        }
        foreach (var key in values.Keys)
        {
            if (!Reserved.Contains(key) && !byParam.ContainsKey(key)) errors.Add(new(key, $"Unknown filter for {resource.Key}"));
        }

        var filters = new List<AdminFilterValue>();
        foreach (var filter in resource.List.Filters)
        {
            var parsed = ParseFilter(filter, resource, values, errors);
            if (parsed is not null) filters.Add(parsed);
        }

        if (errors.Count > 0) throw AdminApiException.InvalidQuery($"Invalid query for {resource.Key}: {errors[0].Field}", errors);
        return new AdminListQuery(page, pageSize, q, sort, filters);
    }

    private static AdminFilterValue? ParseFilter(FilterDescriptor filter, ResourceDescriptor resource, Dictionary<string, List<string>> values,
        List<AdminFieldError> errors)
    {
        if (filter.Type == FilterTypes.DateRange)
        {
            var from = ParseDate(values.GetValueOrDefault($"{filter.Key}From"), $"{filter.Key}From", endOfDay: false, errors);
            var to = ParseDate(values.GetValueOrDefault($"{filter.Key}To"), $"{filter.Key}To", endOfDay: true, errors);
            return from is not null || to is not null ? new DateRangeFilterValue(filter.Key, from, to) : null;
        }

        if (!values.TryGetValue(filter.Key, out var list)) return null;

        switch (filter.Type)
        {
            case FilterTypes.Enum:
            {
                if (list.Count > 1 && filter.Multiple != true)
                {
                    errors.Add(new(filter.Key, "Only one value allowed"));
                    return null;
                }
                var options = filter.Options ?? resource.Fields.FirstOrDefault(f => f.Key == filter.Key)?.Enum ?? [];
                var allowed = options.Select(o => o.Value).ToHashSet(StringComparer.Ordinal);
                var given = list.SelectMany(v => v.Split(',')).Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
                if (given.Count > 1 && filter.Multiple != true)
                {
                    errors.Add(new(filter.Key, "Only one value allowed"));
                    return null;
                }
                var invalid = given.Where(v => !allowed.Contains(v)).ToList();
                if (invalid.Count > 0)
                {
                    errors.Add(new(filter.Key, $"Invalid value: {string.Join(", ", invalid)}"));
                    return null;
                }
                return given.Count > 0 ? new EnumFilterValue(filter.Key, given.Distinct().ToList()) : null;
            }
            case FilterTypes.Boolean:
            {
                if (list.Count > 1 || (list[0] != "true" && list[0] != "false"))
                {
                    errors.Add(new(filter.Key, "Use true or false"));
                    return null;
                }
                return new BooleanFilterValue(filter.Key, list[0] == "true");
            }
            case FilterTypes.Ref:
            {
                var value = list[0].Trim();
                if (list.Count > 1 || value.Length == 0)
                {
                    errors.Add(new(filter.Key, "One id expected"));
                    return null;
                }
                return new RefFilterValue(filter.Key, value);
            }
            default:
                errors.Add(new(filter.Key, $"Unsupported filter type {filter.Type}"));
                return null;
        }
    }

    private static AdminSort ParseSort(string raw, ResourceDescriptor resource, List<AdminFieldError> errors)
    {
        var sort = AdminSort.Parse(raw);
        if (resource.List.Sortable.Contains(sort.Key)) return sort;

        errors.Add(new("sort", $"Can't sort {resource.Key} by {sort.Key}"));
        return AdminSort.Parse(resource.List.DefaultSort);
    }

    private static int ParsePositiveInt(string? raw, int fallback, string field, List<AdminFieldError> errors)
    {
        if (raw is null) return fallback;
        if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 1)
        {
            errors.Add(new(field, "Must be a positive integer"));
            return fallback;
        }
        return (int)Math.Min(value, int.MaxValue);
    }

    // "2026-10-16" (the whole day: from 00:00 or up to 23:59:59.999999 UTC) or a full ISO date.
    private static DateTime? ParseDate(List<string>? list, string field, bool endOfDay, List<AdminFieldError> errors)
    {
        if (list is null) return null;
        if (list.Count > 1)
        {
            errors.Add(new(field, "Only one value allowed"));
            return null;
        }

        var raw = list[0].Trim();
        if (DateOnlyPattern().IsMatch(raw)
            && DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var day))
        {
            day = DateTime.SpecifyKind(day, DateTimeKind.Utc);
            // Postgres keeps microseconds: the last instant of the day.
            return endOfDay ? day.AddDays(1).AddTicks(-10) : day;
        }

        if (raw.Length > 0 && IsoDateTimePattern().IsMatch(raw)
            && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant))
        {
            return instant.UtcDateTime;
        }

        errors.Add(new(field, "Use an ISO date (YYYY-MM-DD)"));
        return null;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex DateOnlyPattern();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}")]
    private static partial Regex IsoDateTimePattern();
}
