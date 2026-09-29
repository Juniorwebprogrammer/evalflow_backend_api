using System.ComponentModel.DataAnnotations;

namespace evalflow_backend_api.Domain.Entities;

/// <summary>
/// A subscription plan and the limits it grants a company. A null limit
/// means unlimited. Rows are seeded from <see cref="Constants.PlanCatalog"/>
/// and can be tuned in the database without a deploy.
/// </summary>
public class Plan
{
    public int Id { get; set; }

    [MaxLength(20)]
    public required string Code { get; set; }

    [MaxLength(40)]
    public required string Nombre { get; set; }

    /// <summary>Active accounts, pending invitations included.</summary>
    public int? MaxEmployees { get; set; }

    /// <summary>Cycles active at the same time.</summary>
    public int? MaxActiveCycles { get; set; }

    /// <summary>Cycles starting in the same calendar year.</summary>
    public int? MaxCyclesPerYear { get; set; }

    /// <summary>Templates the company creates (the default ones don't count).</summary>
    public int? MaxCustomTemplates { get; set; }

    public int? MaxDepartments { get; set; }

    /// <summary>Access to the AI features (AI evaluation analysis).</summary>
    public bool HasAiFeatures { get; set; }

    /// <summary>AI analyses the company can request per calendar month.</summary>
    public int? MaxAiAnalysesPerMonth { get; set; }
}
