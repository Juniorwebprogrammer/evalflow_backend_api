using evalflow_backend_api.Features.AdminApi.Contract;

namespace evalflow_backend_api.Features.AdminApi.Resources;

public sealed record AdminListResult(IReadOnlyList<object> Items, int Total);

/// <summary>
/// A resource of the Admin API (companies, users…): its descriptor for the manifest, its list (the query already
/// validated against the descriptor), its detail, its actions and its edit. Items are records serialized in camelCase with the
/// keys of the descriptor's fields, plus id. Never secrets: no tokens, API keys, password hashes or encrypted data.
/// </summary>
public interface IAdminResource
{
    /// <summary>With its actions and edit; the manifest drops them for callers that can't write.</summary>
    ResourceDescriptor Descriptor { get; }

    Task<AdminListResult> ListAsync(AdminListQuery query, CancellationToken cancellationToken);

    /// <summary>Null when the id doesn't exist (or isn't visible through the Admin API).</summary>
    Task<object?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>Runs an action declared in the descriptor, with its inputs already validated. Returns the updated item.</summary>
    Task<object> RunActionAsync(string action, string id, IReadOnlyDictionary<string, object?> inputs, CancellationToken cancellationToken) =>
        throw AdminApiException.NotFound($"{Descriptor.Key} has no action {action}");

    /// <summary>
    /// Applies the fields of PATCH /{resource}/{id}, already validated against Descriptor.Edit (only the ones given;
    /// null empties an optional one). 404 if the record doesn't exist, 409 if the change breaks a rule. Returns the
    /// updated item.
    /// </summary>
    Task<object> EditAsync(string id, IReadOnlyDictionary<string, object?> changes, CancellationToken cancellationToken) =>
        throw AdminApiException.NotFound($"{Descriptor.Key} can't be edited");
}

/// <summary>Every resource, in manifest order.</summary>
public class AdminResourceCatalog(IEnumerable<IAdminResource> resources)
{
    public IReadOnlyList<IAdminResource> All { get; } = resources.ToList();

    public IAdminResource Require(string key) =>
        All.FirstOrDefault(r => r.Descriptor.Key == key) ?? throw AdminApiException.NotFound($"Unknown resource {key}");
}

internal static class AdminQueryExtensions
{
    /// <summary>Integer ids travel as strings; anything else simply doesn't exist.</summary>
    public static int? ParseId(string id) =>
        int.TryParse(id, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : null;

    /// <summary>A ref filter's id. Not an id → 400 invalid_query (an invalid value is never ignored).</summary>
    public static int RefId(RefFilterValue filter) =>
        ParseId(filter.Value) ?? throw AdminApiException.InvalidQuery($"Invalid id for {filter.Key}", [new(filter.Key, "Must be a numeric id")]);

    public static IQueryable<T> Page<T>(this IQueryable<T> source, AdminListQuery query) =>
        query.Skip is { } skip ? source.Skip(skip).Take(query.PageSize) : source.Take(0);
}
