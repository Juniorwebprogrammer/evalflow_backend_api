using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace evalflow_backend_api.Features.Departments.CreateDepartment;

public static class CreateDepartmentEndpoint
{
    public static void MapCreateDepartment(this IEndpointRouteBuilder app)
    {
        app.MapPost("/departments", async ([FromBody] CreateDepartmentRecord request, IMediator mediator) => await mediator.Send(request))
            .WithTags("Departments")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}