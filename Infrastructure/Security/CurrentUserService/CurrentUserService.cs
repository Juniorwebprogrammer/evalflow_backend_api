using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Infrastructure.Security.CurrentUserService;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? GetEmail() => GetClaimValue(ClaimTypes.Email);

    public string? GetUserId() => GetClaimValue(ClaimTypes.NameIdentifier);

    public string? GetIdentificationId() => GetClaimValue("identificationId");
    
    public string? GetRol() => GetClaimValue(ClaimTypes.Role);

    private string? GetClaimValue(string? claimType)
    {
        Debug.Assert(claimType != null, nameof(claimType) + " != null");
        return httpContextAccessor.HttpContext?.User?.FindFirstValue(claimType);
    }
}