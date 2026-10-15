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
    bool HasAiFeatures,
    int? MaxAiAnalysesPerMonth)
{
    public static PlanDto From(Plan plan) => new(plan.Id, plan.Code, plan.Nombre, plan.MaxEmployees,
        plan.MaxActiveCycles, plan.MaxCyclesPerYear, plan.MaxCustomTemplates, plan.MaxDepartments, plan.HasAiFeatures,
        plan.MaxAiAnalysesPerMonth);
}

public record PlanUsageDto(int Employees, int ActiveCycles, int CyclesThisYear, int CustomTemplates, int Departments, int AiAnalysesThisMonth);

/// <summary>
/// Where the plan stands (from Vexxen): Status (trialing, active, canceling, past_due or none = read-only),
/// AccessEndsAt (canceling/past_due: until when), TrialEndsAt, Manual (set by an Administrator, e.g. Enterprise)
/// and where to manage it (the Vexxen catalog / billing).
/// </summary>
public record PlanStatusDto(string Status, bool HasAccess, DateTime? AccessEndsAt, DateTime? TrialEndsAt, bool Manual, string ManageUrl);

/// <summary>The caller's company plan, its status and how much of each limit it uses.</summary>
public record CompanyPlanDto(PlanDto Plan, PlanUsageDto Usage, PlanStatusDto Status);

public record GetPlansRecord : IRequest<IResult>;

public record GetCompanyPlanRecord : IRequest<IResult>;

/// <summary>
/// Administrator-only. A PlanId sets that plan by hand (Enterprise, sold by the sales team): active, and the sync
/// with Vexxen leaves it alone. PlanId null hands the company back to its Vexxen subscription.
/// </summary>
public record ChangeCompanyPlanRecord(string IdentificationId, int? PlanId) : IRequest<IResult>;

public record ChangeCompanyPlanBody(int? PlanId);
