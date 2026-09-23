using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace evalflow_backend_api.Features.Onboarding.RegisterOwner;

public static class RegisterOwnerEndpoint
{
    public static void MapRegisterOwner(this IEndpointRouteBuilder app)
    {
        app.MapPost("/Onboarding/register-owner", async ([FromBody] RegisterOwnerRecord record, ISender sender) =>
        {
            return await sender.Send(record);
        })
        .WithTags("Onboarding")
        .WithName("RegisterOwner")
        .WithSummary("Registra al usaurio propietario y su empresa")
        .WithDescription("Crea una empresa y un usuario administrador")
        .AddEndpointFilter<ApiKeyEndpointFilter>();
    }
}