using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Departments.GetDepartment;

public static class GetDepartmentEndpoint
{
    public static void MapGetDepartment(this IEndpointRouteBuilder app)
    {
        app.MapGet("/departments/{id:int}", async (int id, IMediator mediator) =>
            {
                return await mediator.Send(new GetDepartmentRecord(id));
            })
            .WithTags("Departments")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}