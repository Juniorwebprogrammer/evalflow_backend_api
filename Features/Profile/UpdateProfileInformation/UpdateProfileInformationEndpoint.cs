using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace evalflow_backend_api.Features.Profile.UpdateProfileInformation;

public static class UpdateProfileEndpoint
{
    public static void MapUpdateProfile(this IEndpointRouteBuilder app)
    {
        app.MapPut("/Profile/update", async ([FromBody] UpdateProfileInformationRecord record, ISender sender) => await sender.Send(record))
            .WithTags("Profile")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}