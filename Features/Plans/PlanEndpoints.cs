using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace evalflow_backend_api.Features.Plans;

public static class PlanEndpoints
{
    public static void MapPlans(this IEndpointRouteBuilder app)
    {
        // Public: the sign-up form lists the plans before there is an account.
        app.MapGet("/plans", async (ISender sender) => await sender.Send(new GetPlansRecord()))
            .WithTags("Plans")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .AllowAnonymous();

        app.MapGet("/Company/plan", async (ISender sender) => await sender.Send(new GetCompanyPlanRecord()))
            .WithTags("Plans")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh},{AppRoles.Administrator}" });

        app.MapPut("/Company/{identificationId}/plan",
                async (string identificationId, [FromBody] ChangeCompanyPlanBody body, ISender sender) =>
                    await sender.Send(new ChangeCompanyPlanRecord(identificationId, body.PlanId)))
            .WithTags("Plans")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = AppRoles.Administrator });
    }
}
