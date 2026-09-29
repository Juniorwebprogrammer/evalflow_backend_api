using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.AiAnalysis.GetCycleAiAnalyses;

public class GetCycleAiAnalysesHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService,
    ILogger<GetCycleAiAnalysesHandler> logger
) : IRequestHandler<GetCycleAiAnalysesRecord, IResult>
{
    public async Task<IResult> Handle(GetCycleAiAnalysesRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var cycleExists = await dbContext.EvaluationCycles
            .AnyAsync(c => c.Id == request.CycleId && c.Empresa!.IdentificationId == tenantId, cancellationToken);
        if (!cycleExists) return Results.NotFound(new { Message = "Ciclo no encontrado." });

        var query = dbContext.AiEvaluationAnalyses.AsNoTracking().Where(a => a.EvaluationCycleId == request.CycleId);
        if (request.EvaluatedUserId.HasValue) query = query.Where(a => a.EvaluatedUserId == request.EvaluatedUserId.Value);

        var analyses = await query.ToListAsync(cancellationToken);

        var latest = analyses
            .GroupBy(a => (a.TemplateId, a.EvaluatedUserId))
            .Select(g => g.OrderByDescending(a => a.FechaCreacion).ThenByDescending(a => a.Id).First())
            .OrderBy(a => a.EvaluatedUserId)
            .ThenBy(a => a.TemplateId)
            .Select(a => AiAnalysisMapper.ToDto(a, encryptionService, logger))
            .ToList();

        return Results.Ok(latest);
    }
}
