using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace evalflow_backend_api.Features.Questions.CreateQuestion;

public static class CreateQuestionEndpoint
{
    public static void MapCreateQuestion(this IEndpointRouteBuilder app)
    {
        app.MapPost("/templates/{templateId:int}/questions", async (int templateId, [FromBody] CreateQuestionBody body, IMediator mediator) =>
            await mediator.Send(new CreateQuestionRecord(templateId, body.Texto, body.Topic, body.Tipo, body.Opciones, body.Orden)))
            .WithTags("Questions")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{AppRoles.Owner},{AppRoles.Rrhh}" });
    }
}