using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace evalflow_backend_api.Features.Profile.Avatar;

public static class AvatarEndpoints
{
    /// <summary>GET / PUT / DELETE /Profile/avatar — the caller's own profile picture.</summary>
    public static void MapProfileAvatar(this IEndpointRouteBuilder app)
    {
        app.MapGet("/Profile/avatar", async (ISender sender) => await sender.Send(new GetAvatarRecord()))
            .WithTags("Profile")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();

        app.MapPut("/Profile/avatar", async ([FromBody] UploadAvatarRecord record, ISender sender) => await sender.Send(record))
            .WithTags("Profile")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();

        app.MapDelete("/Profile/avatar", async (ISender sender) => await sender.Send(new DeleteAvatarRecord()))
            .WithTags("Profile")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}
