using evalflow_backend_api.Features.EvaluationSubmissions;
using evalflow_backend_api.Features.EvaluationSubmissions.EvaluationSubmissionsDto; // Para usar el DTO
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GetMyCompletedSubmissions;

public class GetMyCompletedSubmissionsHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetMyCompletedSubmissionsRecord, IResult>
{
    public async Task<IResult> Handle(GetMyCompletedSubmissionsRecord request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Results.Unauthorized();

        var completedSubmissions = await FetchCompletedSubmissionsAsync(userId.Value, cancellationToken);

        return Results.Ok(completedSubmissions);
    }

    private int? GetUserId()
    {
        var userIdStr = currentUser.GetUserId();
        return int.TryParse(userIdStr, out var userId) ? userId : null;
    }

    private async Task<List<PendingSubmissionDto>> FetchCompletedSubmissionsAsync(int respondentUserId, CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationSubmissions
            .AsNoTracking()
            .Where(s => s.RespondentUserId == respondentUserId && s.IsCompleted)
            .OrderByDescending(s => s.SubmittedAt) // Ordenamos por fecha de entrega más reciente
            .Select(s => new PendingSubmissionDto(
                s.Id,
                s.Template!.Titulo,
                s.Cycle!.Nombre,
                $"{s.EvaluatedUser!.Nombre} {s.EvaluatedUser.Apellidos}",
                s.Cycle.FechaFin
            ))
            .ToListAsync(cancellationToken);
    }
}