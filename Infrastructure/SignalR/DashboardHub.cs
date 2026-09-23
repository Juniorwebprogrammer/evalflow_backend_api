using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace evalflow_backend_api.Infrastructure.SignalR;

[Authorize]
public class DashboardHub(ILogger<DashboardHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = GetTenantId();

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, tenantId);
            logger.LogInformation(
                "DashboardHub: conexión {ConnectionId} unida al grupo del tenant {TenantId}.",
                Context.ConnectionId, tenantId);
        }
        else
        {
            logger.LogWarning(
                "DashboardHub: conexión {ConnectionId} autenticada pero sin claim 'identificationId' — no se unió a ningún grupo.",
                Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = GetTenantId();

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, tenantId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Reads the claim straight off `Context.User` instead of going through
    // `ICurrentUserService` (which resolves it via `IHttpContextAccessor`).
    // That accessor reflects the ASP.NET Core request pipeline's ambient
    // HttpContext, which isn't reliably available for a Hub's connection
    // lifecycle events over WebSockets — using `Context.User` directly is
    // the pattern Microsoft's own SignalR auth docs recommend, and the only
    // one guaranteed to see the identity that authenticated this connection.
    private string? GetTenantId() => Context.User?.FindFirstValue("identificationId");
}