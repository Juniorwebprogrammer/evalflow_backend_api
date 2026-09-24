using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationSubmissions.DeleteSubmission;

public class DeleteSubmissionHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<DeleteSubmissionRecord, IResult>
{
    public async Task<IResult> Handle(DeleteSubmissionRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var submission = await GetSubmissionSecurelyAsync(request.SubmissionId, tenantId, cancellationToken);
        if (submission is null) return Results.NotFound(new { Message = "Formulario no encontrado." });

        if (submission.Cycle!.FechaCompletado.HasValue)
            return Results.Conflict(new { Message = "El ciclo ya se ha completado y no admite cambios." });

        await DeleteAndSaveAsync(submission, cancellationToken);

        return Results.Ok(new { Message = "Formulario eliminado correctamente." });
    }

    private async Task<EvaluationSubmission?> GetSubmissionSecurelyAsync(int submissionId, string tenantId, CancellationToken cancellationToken)
    {
        // Aseguramos que el formulario pertenece a la empresa del administrador que lo intenta borrar
        return await dbContext.EvaluationSubmissions
            .Include(s => s.Cycle)
            .ThenInclude(c => c!.Empresa)
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.Cycle!.Empresa!.IdentificationId == tenantId, cancellationToken);
    }

    private async Task DeleteAndSaveAsync(EvaluationSubmission submission, CancellationToken cancellationToken)
    {
        dbContext.EvaluationSubmissions.Remove(submission);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}