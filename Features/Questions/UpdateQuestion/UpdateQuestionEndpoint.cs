using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Questions.UpdateQuestion;

public static class UpdateQuestionEndpoint
{
    public static void MapUpdateQuestion(this IEndpointRouteBuilder app)
    {
        app.MapPut("/templates/{templateId:int}/questions/{questionId:int}", async (int templateId, int questionId, [FromBody] UpdateQuestionBody body, IMediator mediator) =>
            await mediator.Send(new UpdateQuestionRecord(templateId, questionId, body.Texto, body.Topic, body.Tipo, body.Opciones, body.Orden)))
            .WithTags("Questions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}