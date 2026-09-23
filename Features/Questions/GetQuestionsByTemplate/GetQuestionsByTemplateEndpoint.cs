using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Questions.GetQuestionsByTemplate;

public static class GetQuestionsByTemplateEndpoint
{
    public static void MapGetQuestionsByTemplate(this IEndpointRouteBuilder app)
    {
        app.MapGet("/templates/{templateId:int}/questions", async (int templateId, IMediator mediator) =>
                await mediator.Send(new GetQuestionsByTemplateRecord(templateId)))
            .WithTags("Questions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization();
    }
}