using evalflow_backend_api.Infrastructure.Security;
using MediatR;

namespace evalflow_backend_api.Features.Profile.GetProfileInformation;

public static class GetProfileInformationEndpoint
{
    public static void MapGetProfile(this IEndpointRouteBuilder app)
    {
        app.MapGet("/Profile/me", async (ISender sender) => await sender.Send(new GetProfileInformationRecord()))
            .WithTags("Profile")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}