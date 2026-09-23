using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Questions.GetQuestionsByTemplate;

public class GetQuestionsByTemplateHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetQuestionsByTemplateRecord, IResult>
{
    public async Task<IResult> Handle(GetQuestionsByTemplateRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var templateExists = await VerifyTemplateOwnershipAsync(request.TemplateId, tenantId, cancellationToken);
        if (!templateExists) return Results.NotFound(new { Message = "La plantilla no existe o no pertenece a tu empresa." });

        var questions = await FetchQuestionsAsync(request.TemplateId, cancellationToken);

        return Results.Ok(questions);
    }

    private async Task<bool> VerifyTemplateOwnershipAsync(int templateId, string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Templates
            .Include(t => t.Empresa)
            .AnyAsync(t => t.Id == templateId && t.Empresa!.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<List<QuestionDto.QuestionDto>> FetchQuestionsAsync(int templateId, CancellationToken cancellationToken)
    {
        return await dbContext.Questions
            .AsNoTracking()
            .Where(q => q.TemplateId == templateId)
            .OrderBy(q => q.Orden) // Devolvemos las preguntas ya ordenadas para el Frontend
            .Select(q => new QuestionDto.QuestionDto(
                q.Id,
                q.Texto,
                q.Tipo,
                q.Topic,
                q.Opciones,
                q.Orden
            ))
            .ToListAsync(cancellationToken);
    }
}