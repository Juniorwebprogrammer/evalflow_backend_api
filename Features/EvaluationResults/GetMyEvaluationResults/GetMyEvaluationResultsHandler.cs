using evalflow_backend_api.Features.EvaluationResults.EvaluationResultsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationResults.GetMyEvaluationResults;

public class GetMyEvaluationResultsHandler(AppDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<GetMyEvaluationResultsRecord, IResult>
{
    public async Task<IResult> Handle(GetMyEvaluationResultsRecord request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(currentUser.GetUserId(), out var userId)) return Results.Unauthorized();

        var results = await dbContext.EvaluationResults
            .AsNoTracking()
            .Where(r => r.EvaluatedUserId == userId)
            .OrderByDescending(r => r.CompletedAt)
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
