using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Questions.DeleteQuestion;

public static class DeleteQuestionEndpoint
{
    public static void MapDeleteQuestion(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/templates/{templateId:int}/questions/{questionId:int}", async (int templateId, int questionId, IMediator mediator) =>
                await mediator.Send(new DeleteQuestionRecord(templateId, questionId)))
            .WithTags("Questions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}