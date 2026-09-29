using evalflow_backend_api.Domain.Entities;

namespace evalflow_backend_api.Domain.Constants;

/// <summary>
/// The plans EvalFlow sells. Seeds the <c>Plans</c> table and is the fallback
/// when a plan row is missing, so limits are never silently lifted.
/// </summary>
public static class PlanCatalog
{
    public const int Starter = 1;
    public const int Growth = 2;
    public const int Enterprise = 3;

    public static readonly IReadOnlyList<Plan> All =
    [
        new()
        {
            Id = Starter, Code = "starter", Nombre = "Starter",
            MaxEmployees = 25, MaxActiveCycles = 1, MaxCyclesPerYear = 4,
            MaxCustomTemplates = 10, MaxDepartments = 5, HasAiFeatures = false,
            MaxAiAnalysesPerMonth = 0,
        },
        new()
        {
            Id = Growth, Code = "growth", Nombre = "Growth",
            MaxEmployees = 100, MaxActiveCycles = 3, MaxCyclesPerYear = 12,
            MaxCustomTemplates = 50, MaxDepartments = 20, HasAiFeatures = true,
            MaxAiAnalysesPerMonth = 30,
        },
        new()
        {
            Id = Enterprise, Code = "enterprise", Nombre = "Enterprise",
            MaxEmployees = null, MaxActiveCycles = null, MaxCyclesPerYear = null,
            MaxCustomTemplates = null, MaxDepartments = null, HasAiFeatures = true,
            MaxAiAnalysesPerMonth = 150,
        },
    ];

    public static bool Exists(int planId) => All.Any(p => p.Id == planId);

    /// <summary>The catalog entry for <paramref name="planId"/>, or Starter (the most restrictive) when unknown.</summary>
    public static Plan Get(int planId) => All.FirstOrDefault(p => p.Id == planId) ?? All[0];
}
