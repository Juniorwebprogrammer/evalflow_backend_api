using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Team.InviteEmployee;

public static class InviteEmployeeEndpoint
{
    public static void MapInviteEmployee(this IEndpointRouteBuilder app)
    {
        app.MapPost("/Team/invite", async ([FromBody] InviteEmployeeRecord record, ISender sender) => await sender.Send(record))
            .WithTags("Team")
            .WithName("InviteEmployee")
            .WithSummary("Invitar a un nuevo empleado a la empresa")
            .WithDescription("Requiere API Key y JWT válido. Restringido a Owner, RRHH y Administrator global.")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Administrator},{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}