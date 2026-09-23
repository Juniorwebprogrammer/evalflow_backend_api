using evalflow_backend_api.Infrastructure.Security;
using MediatR;

namespace evalflow_backend_api.Features.Companies.GetByIdentificationId;

public static class GetCompanyByIdentificationIdEndpoint
{
    public static void MapGetCompanyByIdentificationId(this IEndpointRouteBuilder app)
    {
        app.MapGet("/Company/get/by-identification/{identificationId}", async (string identificationId, ISender sender) =>
            {
                var company = await sender.Send(new GetCompanyByIdentificationIdRecord(identificationId));
            
                return company is not null
                    ? Results.Ok(company)
                    : Results.NotFound($"No se encontró ninguna empresa con este identificationId: {identificationId}");
            })
            .WithTags("Companies")
            .WithName("GetCompanyByIdentificationId")
            .WithSummary("Gets a company by identificationId")
            .WithDescription("Gets a company by identificationId")
            .AddEndpointFilter<ApiKeyEndpointFilter>();;
    }
}