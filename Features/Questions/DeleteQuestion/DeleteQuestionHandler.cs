using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Questions.DeleteQuestion;

public class DeleteQuestionHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<DeleteQuestionRecord, IResult>
{
    public async Task<IResult> Handle(DeleteQuestionRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var question = await GetSecureQuestionAsync(request.TemplateId, request.QuestionId, tenantId, cancellationToken);
        if (question is null) return Results.NotFound(new { Message = "Pregunta no encontrada o acceso denegado." });

        await DeleteAndSaveQuestionAsync(question, cancellationToken);

        return Results.Ok(new { Message = "Pregunta eliminada correctamente." });
    }

    private async Task<Question?> GetSecureQuestionAsync(int templateId, int questionId, string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Questions
            .Include(q => q.Template)
            .ThenInclude(t => t!.Empresa)
            .FirstOrDefaultAsync(q => q.Id == questionId && q.TemplateId == templateId && q.Template!.Empresa!.IdentificationId == tenantId, cancellationToken);
    }

    private async Task DeleteAndSaveQuestionAsync(Question question, CancellationToken cancellationToken)
    {
        dbContext.Questions.Remove(question);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}