using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationCycles.UpdateEvaluationCycle;

public class UpdateEvaluationCycleHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<UpdateEvaluationCycleRecord, IResult>
{
    public async Task<IResult> Handle(UpdateEvaluationCycleRecord request, CancellationToken cancellationToken)
    {
        if (request.FechaInicio >= request.FechaFin)
            return Results.BadRequest(new { Message = "La fecha de inicio debe ser anterior a la fecha de fin." });

        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) return Results.Unauthorized();

        var cycle = await GetCycleAsync(request.Id, company.Id, cancellationToken);
        if (cycle is null) return Results.NotFound(new { Message = "Ciclo no encontrado." });

        await UpdateAndSaveCycleAsync(cycle, request, cancellationToken);

        return Results.Ok(new { Message = "Ciclo actualizado correctamente." });
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

    private async Task UpdateAndSaveCycleAsync(EvaluationCycle cycle, UpdateEvaluationCycleRecord request, CancellationToken cancellationToken)
    {
        cycle.Nombre = request.Nombre;
        cycle.Descripcion = request.Descripcion;
        cycle.Activo = request.Activo;
        cycle.FechaInicio = request.FechaInicio.ToUniversalTime();
        cycle.FechaFin = request.FechaFin.ToUniversalTime();
        cycle.TipoEvaluación = request.TipoEvaluacion;

        dbContext.EvaluationCycles.Update(cycle);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}