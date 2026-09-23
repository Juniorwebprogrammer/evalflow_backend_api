using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace evalflow_backend_api.Features.Profile.DeleteAccount;

public static class DeleteAccountEndpoint
{
    public static void MapDeleteAccount(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/Profile/delete",
                async ([FromBody] DeleteAccountRecord request, ISender sender) => await sender.Send(request))
            .WithTags("Profile")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}