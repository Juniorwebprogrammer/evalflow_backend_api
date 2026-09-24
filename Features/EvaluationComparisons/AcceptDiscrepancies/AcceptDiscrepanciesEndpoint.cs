using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.EvaluationComparisons.AcceptDiscrepancies;

public static class AcceptDiscrepanciesEndpoint
{
    public static void MapAcceptDiscrepancies(this IEndpointRouteBuilder app)
    {
        app.MapPut("/evaluation-cycles/{cycleId:int}/discrepancies/acceptances", async (int cycleId, [FromBody] AcceptDiscrepanciesBody body, IMediator mediator) =>
                await mediator.Send(new AcceptDiscrepanciesRecord(cycleId, body.EvaluatedUserId, body.TemplateId, body.QuestionIds ?? [], body.Source)))
            .WithTags("Evaluation Comparisons")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}
