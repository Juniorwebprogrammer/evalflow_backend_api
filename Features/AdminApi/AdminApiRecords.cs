using System.Text.Json;
using evalflow_backend_api.Features.AdminApi.Access;
using MediatR;

namespace evalflow_backend_api.Features.AdminApi;

/// <summary>GET /admin/v1/manifest. The actions only for callers that can run them (contract §12.2).</summary>
public record GetAdminManifestRecord(bool CanWrite) : IRequest<IResult>;

/// <summary>GET /admin/v1/health: always 200, the status goes in the body.</summary>
public record GetAdminHealthRecord : IRequest<IResult>;

/// <summary>GET /admin/v1/stats: the cards of the app's home panel.</summary>
public record GetAdminStatsRecord : IRequest<IResult>;

/// <summary>GET /admin/v1/{resource}, with the URL parameters as they come (validated against the resource).</summary>
public record ListAdminResourceRecord(string Resource, IReadOnlyList<KeyValuePair<string, string>> Query) : IRequest<IResult>;

/// <summary>GET /admin/v1/{resource}/{id}.</summary>
public record GetAdminResourceItemRecord(string Resource, string Id) : IRequest<IResult>;

/// <summary>
/// POST /admin/v1/{resource}/{id}/actions/{action} with { inputs }. Body: null when the request had none. Caller and
/// RequestId go to the audit log.
/// </summary>
public record RunAdminActionRecord(string Resource, string Id, string Action, JsonElement? Body, AdminCaller Caller, string RequestId)
    : IRequest<IResult>;
