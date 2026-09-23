using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Questions.CreateQuestion;

public class CreateQuestionHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<CreateQuestionRecord, IResult>
{
    public async Task<IResult> Handle(CreateQuestionRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var templateExists = await VerifyTemplateOwnershipAsync(request.TemplateId, tenantId, cancellationToken);
        if (!templateExists) return Results.NotFound(new { Message = "La plantilla no existe o no pertenece a tu empresa." });

        var newQuestionId = await CreateAndSaveQuestionAsync(request, cancellationToken);

        return Results.Created($"/templates/{request.TemplateId}/questions/{newQuestionId}", new { Message = "Pregunta creada con éxito.", Id = newQuestionId });
    }

    private async Task<bool> VerifyTemplateOwnershipAsync(int templateId, string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Templates
            .Include(t => t.Empresa)
            .AnyAsync(t => t.Id == templateId && t.Empresa!.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<int> CreateAndSaveQuestionAsync(CreateQuestionRecord request, CancellationToken cancellationToken)
    {
        var question = new Question
        {
            Texto = request.Texto,
            Topic = request.Topic,
            Tipo = request.Tipo,
            Opciones = request.Tipo == Domain.Enums.QuestionType.Seleccion ? request.Opciones : null, // Limpiamos opciones si no es de selección
            Orden = request.Orden,
            TemplateId = request.TemplateId
        };

        dbContext.Questions.Add(question);
        await dbContext.SaveChangesAsync(cancellationToken);

        return question.Id;
    }
}