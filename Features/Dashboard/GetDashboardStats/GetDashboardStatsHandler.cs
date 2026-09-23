using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Dashboard.GetDashboardStats;

/// <summary>
/// Aggregated numbers for the dashboard home: active employees, departments,
/// active cycles, and the progress of the currently active cycle (if any).
/// One round-trip so the frontend doesn't have to compose several endpoints.
/// </summary>
public class GetDashboardStatsHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetDashboardStatsRecord, IResult>
{
    public async Task<IResult> Handle(GetDashboardStatsRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var companyId = await GetCompanyIdAsync(tenantId, cancellationToken);
        if (companyId is null) return Results.Unauthorized();

        var activeEmployeesCount = await dbContext.Users
            .CountAsync(u => u.EmpresaID == companyId && u.Activo, cancellationToken);

        var departmentsCount = await dbContext.Departments
            .CountAsync(d => d.EmpresaID == companyId, cancellationToken);

        var cycles = await dbContext.EvaluationCycles
            .AsNoTracking()
            .Where(c => c.EmpresaID == companyId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var activeCycles = cycles
            .Where(c => c.Activo && c.FechaInicio <= now && c.FechaFin >= now)
            .ToList();
        var activeCycle = activeCycles.OrderByDescending(c => c.FechaInicio).FirstOrDefault();

        var (activeCycleDto, pendingCount, completedCount) =
            activeCycle is null
                ? (null, 0, 0)
                : await BuildActiveCycleDtoAsync(activeCycle, cancellationToken);

        var stats = new DashboardStatsDto(
            activeEmployeesCount,
            departmentsCount,
            activeCycles.Count,
            activeCycleDto,
            pendingCount,
            completedCount
        );

        return Results.Ok(stats);
    }

    private async Task<int?> GetCompanyIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);

        return company?.Id;
    }

    private async Task<(DashboardActiveCycleDto? dto, int pendingCount, int completedCount)> BuildActiveCycleDtoAsync(
        EvaluationCycle activeCycle, CancellationToken cancellationToken)
    {
        var submissions = await dbContext.EvaluationSubmissions
            .AsNoTracking()
            .Where(s => s.EvaluationCycleId == activeCycle.Id)
            .Select(s => s.IsCompleted)
            .ToListAsync(cancellationToken);

        // One submission is one form to fill (self-evaluation or
        // manager-on-subordinate), not one person — a respondent can have
        // several (their own self-evaluation plus one per subordinate), so
        // the denominator here has to be the submission count itself, not
        // `Distinct()` on the respondent. Using the distinct count let
        // completed/total exceed 100% once someone with more than one
        // submission finished all of them.
        var totalSubmissions = submissions.Count;
        var completedCount = submissions.Count(isCompleted => isCompleted);
        var pendingCount = totalSubmissions - completedCount;

        var dto = new DashboardActiveCycleDto(
            activeCycle.Id,
            activeCycle.Nombre,
            activeCycle.FechaInicio,
            activeCycle.FechaFin,
            totalSubmissions,
            completedCount,
            pendingCount
        );

        return (dto, pendingCount, completedCount);
    }
}
