using evalflow_backend_api.Features.Departments.GetAllDepartment;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Departments.GetAllDepartments;

public static class GetAllDepartmentsEndpoint
{
    public static void MapGetAllDepartments(this IEndpointRouteBuilder app)
    {
        // Ruta base plural: /departments
        app.MapGet("/departments", async (IMediator mediator) =>
            {
                return await mediator.Send(new GetAllDepartmentsRecord());
            })
            .WithTags("Departments")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}