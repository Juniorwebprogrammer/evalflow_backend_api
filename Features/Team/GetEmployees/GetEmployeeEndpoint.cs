using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace evalflow_backend_api.Features.Team.GetEmployees;

public static class GetEmployeeEndpoint
{
    public static void MapGetEmployees(this IEndpointRouteBuilder app)
    {
        app.MapGet("/Team/list", async (ISender sender) =>
            {
                return await sender.Send(new GetEmployeeRecord());
            })
            .WithTags("Team")
            .WithName("GetEmployees")
            .WithSummary("Gets all employees for tenant id")
            .WithDescription("Gets all employees for tenant id")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute
                { Roles = $"{AppRoles.Administrator},{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}