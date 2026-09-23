using evalflow_backend_api.Features.EvaluationSubmissions.EvaluationSubmissionsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GetMyPendingSubmissions;

public class GetMyPendingSubmissionsHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetMyPendingSubmissionsRecord, IResult>
{
    public async Task<IResult> Handle(GetMyPendingSubmissionsRecord request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Results.Unauthorized();

        var pendingSubmissions = await FetchPendingSubmissionsAsync(userId.Value, cancellationToken);

        return Results.Ok(pendingSubmissions);
    }

    private int? GetUserId()
    {
        var userIdStr = currentUser.GetUserId();
        return int.TryParse(userIdStr, out var userId) ? userId : null;
    }

    private async Task<List<PendingSubmissionDto>> FetchPendingSubmissionsAsync(int respondentUserId, CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationSubmissions
            .AsNoTracking()
            .Where(s => s.RespondentUserId == respondentUserId && !s.IsCompleted && s.Cycle!.Activo)
            .OrderBy(s => s.Cycle!.FechaFin)
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