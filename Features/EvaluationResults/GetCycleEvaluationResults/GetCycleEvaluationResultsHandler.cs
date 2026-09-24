using evalflow_backend_api.Features.EvaluationResults.EvaluationResultsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationResults.GetCycleEvaluationResults;

public class GetCycleEvaluationResultsHandler(AppDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<GetCycleEvaluationResultsRecord, IResult>
{
    public async Task<IResult> Handle(GetCycleEvaluationResultsRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var cycleExists = await dbContext.EvaluationCycles
            .AnyAsync(c => c.Id == request.CycleId && c.Empresa!.IdentificationId == tenantId, cancellationToken);
        if (!cycleExists) return Results.NotFound(new { Message = "Ciclo no encontrado." });

        var results = await dbContext.EvaluationResults
            .AsNoTracking()
            .Where(r => r.EvaluationCycleId == request.CycleId)
            .OrderBy(r => r.EvaluatedUser!.Nombre)
            .ThenBy(r => r.Template!.Titulo)
            .Select(r => new EvaluationResultSummaryDto(
                r.Id,
                r.EvaluationCycleId,
                r.Cycle!.Nombre,
                r.TemplateId,
                r.Template!.Titulo,
                r.EvaluatedUserId,
                r.EvaluatedUser!.Nombre + " " + r.EvaluatedUser.Apellidos,
                r.CompletedAt,
                r.AverageFinal
            ))
            .ToListAsync(cancellationToken);

        return Results.Ok(results);
    }
}
