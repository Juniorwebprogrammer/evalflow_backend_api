using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationCycles.DeleteEvaluationCycle;

public class DeleteEvaluationCycleHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<DeleteEvaluationCycleRecord, IResult>
{
    public async Task<IResult> Handle(DeleteEvaluationCycleRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) return Results.Unauthorized();

        var cycle = await GetCycleAsync(request.Id, company.Id, cancellationToken);
        if (cycle is null) return Results.NotFound(new { Message = "Ciclo no encontrado." });

        if (cycle.FechaCompletado.HasValue)
            return Results.BadRequest(new { Message = "No se puede eliminar un ciclo completado: contiene los resultados de las evaluaciones." });

        if (cycle.Activo) return Results.BadRequest(new { Message = "No se puede eliminar un ciclo activo. Desactívalo primero." });

        await DeleteAndSaveCycleAsync(cycle, cancellationToken);

        return Results.Ok(new { Message = "Ciclo eliminado correctamente." });
    }

    private async Task<Company?> GetCompanyAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Companies.FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<EvaluationCycle?> GetCycleAsync(int cycleId, int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationCycles
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.EmpresaID == companyId, cancellationToken);
    }

    private async Task DeleteAndSaveCycleAsync(EvaluationCycle cycle, CancellationToken cancellationToken)
    {
        dbContext.EvaluationCycles.Remove(cycle);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}