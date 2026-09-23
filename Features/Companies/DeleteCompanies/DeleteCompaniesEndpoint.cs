using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace evalflow_backend_api.Features.Companies.DeleteCompanies;

public static class DeleteCompanyEndpoint
{
    public static void MapDeleteCompany(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/Company/delete", async ([FromBody] DeleteCompaniesRecord record, ISender sender) => await sender.Send(record))
            .WithTags("Company")
            .WithSummary("Elimina permanentemente la empresa y todos sus datos")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Administrator},{AppRoles.Owner}" });
    }
}