using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Dashboard.GetDashboardStats;

public record GetDashboardStatsRecord() : IRequest<IResult>;

public record DashboardActiveCycleDto(
    int Id,
    string Nombre,
    DateTime FechaInicio,
    DateTime FechaFin,
    int TotalSubmissions,
    int CompletedCount,
    int PendingCount
);

public record DashboardStatsDto(
    int ActiveEmployeesCount,
    int DepartmentsCount,
    int ActiveCyclesCount,
    DashboardActiveCycleDto? ActiveCycle,
    int TotalPendingSubmissions,
    int TotalCompletedSubmissions
);
