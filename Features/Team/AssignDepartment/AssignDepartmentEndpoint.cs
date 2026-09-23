using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace evalflow_backend_api.Features.Team.AssignDepartment;

public record AssignDepartmentBody(int? DepartmentId);

public static class AssignDepartmentEndpoint
{
    public static void MapAssignDepartment(this IEndpointRouteBuilder app)
    {
        app.MapPut("/team/{userId:int}/department", async (int userId, [FromBody] DepartmentAssignBody body, IMediator mediator) =>
            {
                var command = new AssignDepartmentRecord(userId, body.DepartmentId);
                return await mediator.Send(command);
            })
            .WithTags("Team")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}