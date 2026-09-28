using evalflow_backend_api.Domain.Entities;
using MediatR;

namespace evalflow_backend_api.Features.Plans;

/// <summary>A plan's limits (null = unlimited) and features.</summary>
public record PlanDto(
    int Id,
    string Code,
    string Nombre,
    int? MaxEmployees,
    int? MaxActiveCycles,
    int? MaxCyclesPerYear,
    int? MaxCustomTemplates,
    int? MaxDepartments,
    bool HasAiFeatures)
{
    public static PlanDto From(Plan plan) => new(plan.Id, plan.Code, plan.Nombre, plan.MaxEmployees,
        plan.MaxActiveCycles, plan.MaxCyclesPerYear, plan.MaxCustomTemplates, plan.MaxDepartments, plan.HasAiFeatures);
}

public record PlanUsageDto(int Employees, int ActiveCycles, int CyclesThisYear, int CustomTemplates, int Departments);

/// <summary>The caller's company plan plus how much of each limit it uses.</summary>
public record CompanyPlanDto(PlanDto Plan, PlanUsageDto Usage);

public record GetPlansRecord : IRequest<IResult>;

public record GetCompanyPlanRecord : IRequest<IResult>;

/// <summary>Administrator-only: moves a company to another plan (no payment gateway yet).</summary>
public record ChangeCompanyPlanRecord(string IdentificationId, int PlanId) : IRequest<IResult>;

public record ChangeCompanyPlanBody(int PlanId);
