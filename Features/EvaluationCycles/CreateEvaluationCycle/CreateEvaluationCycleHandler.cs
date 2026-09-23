using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationCycles.CreateEvaluationCycle;

public class CreateEvaluationCycleHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<CreateEvaluationCycleRecord, IResult>
{
    public async Task<IResult> Handle(CreateEvaluationCycleRecord request, CancellationToken cancellationToken)
    {
        if (request.FechaInicio >= request.FechaFin)
            return Results.BadRequest(new { Message = "La fecha de inicio debe ser anterior a la fecha de fin." });

        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) return Results.Unauthorized();

        var cycleId = await CreateAndSaveCycleAsync(request, company.Id, cancellationToken);

        return Results.Created($"/evaluation-cycles/{cycleId}", new { Message = "Ciclo de evaluación creado con éxito.", Id = cycleId });
    }

    private async Task<Company?> GetCompanyAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Companies
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<int> CreateAndSaveCycleAsync(CreateEvaluationCycleRecord request, int companyId, CancellationToken cancellationToken)
    {
        var cycle = new EvaluationCycle
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            FechaInicio = request.FechaInicio.ToUniversalTime(),
            FechaFin = request.FechaFin.ToUniversalTime(),
            Activo = false,
            EmpresaID = companyId,
            TipoEvaluación = request.TipoEvaluacion
        };

        dbContext.EvaluationCycles.Add(cycle);
        await dbContext.SaveChangesAsync(cancellationToken);

        return cycle.Id;
    }
}