using evalflow_backend_api.Infrastructure.Security;
using MediatR;

namespace evalflow_backend_api.Features.Companies.GetByName;

public static class GetCompanyByNameEndpoint
{
    public static void MapGetCompanyByName(this IEndpointRouteBuilder app)
    {
        app.MapGet("/Company/get/by-name/{nombre}", async (string nombre, ISender sender) =>
        {
            var company = await sender.Send(new GetCompanyByNameQuery(nombre));
            
            return company is not null
                ? Results.Ok(company)
                : Results.NotFound($"No se encontró ninguna empresa con el {nombre}");
        })
        .WithTags("Companies")
        .WithName("GetCompanyByName")
        .WithSummary("Gets a company by name")
        .WithDescription("Gets a company by name")
        .AddEndpointFilter<ApiKeyEndpointFilter>();
    }
}