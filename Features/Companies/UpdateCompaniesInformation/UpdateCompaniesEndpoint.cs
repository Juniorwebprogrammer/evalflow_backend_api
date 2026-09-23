using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace evalflow_backend_api.Features.Companies.UpdateCompaniesInformation;

public static class UpdateCompaniesEndpoint
{
    public static void MapUpdateCompany(this IEndpointRouteBuilder app)
    {
        app.MapPut("/Company/update",
                async ([FromBody] UpdateCompaniesInformationRecord record, ISender sender) => await sender.Send(record))
            .WithTags("Company")
            .WithSummary("Updates companies information")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute
                { Roles = $"{AppRoles.Administrator}, {AppRoles.Owner}, {AppRoles.Rrhh}" });
    }
}