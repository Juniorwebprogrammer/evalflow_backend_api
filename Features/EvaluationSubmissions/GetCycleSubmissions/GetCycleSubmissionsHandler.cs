using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.EvaluationSubmissions.EvaluationSubmissionsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GetCycleSubmissions;

/// <summary>
/// Lists every submission of a cycle (self + manager evaluations, for every
/// assigned user), for Owner/Rrhh to see who has answered and who hasn't.
/// </summary>
public class GetCycleSubmissionsHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetCycleSubmissionsRecord, IResult>
{
    public async Task<IResult> Handle(GetCycleSubmissionsRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var cycleExists = await CycleBelongsToTenantAsync(request.CycleId, tenantId, cancellationToken);
        if (!cycleExists) return Results.NotFound(new { Message = "Ciclo no encontrado." });

        var submissions = await FetchCycleSubmissionsAsync(request.CycleId, cancellationToken);

        return Results.Ok(submissions);
    }

    private async Task<bool> CycleBelongsToTenantAsync(int cycleId, string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationCycles
            .AsNoTracking()
            .AnyAsync(c => c.Id == cycleId && c.Empresa!.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<List<CycleSubmissionDto>> FetchCycleSubmissionsAsync(int cycleId, CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationSubmissions
            .AsNoTracking()
            .Where(s => s.EvaluationCycleId == cycleId)
            .OrderBy(s => s.RespondentUser!.Nombre)
            .ThenBy(s => s.RespondentUser!.Apellidos)
            .Select(s => new CycleSubmissionDto(
                s.Id,
                s.RespondentUserId,
                $"{s.RespondentUser!.Nombre} {s.RespondentUser.Apellidos}",
                s.EvaluatedUserId,
                $"{s.EvaluatedUser!.Nombre} {s.EvaluatedUser.Apellidos}",
                s.Template!.Titulo,
                s.IsCompleted,
                s.SubmittedAt
            ))
            .ToListAsync(cancellationToken);
    }
}
