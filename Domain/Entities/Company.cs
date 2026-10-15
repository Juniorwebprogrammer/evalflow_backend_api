using System.ComponentModel.DataAnnotations;

namespace evalflow_backend_api.Domain.Entities;

public class Company
{
    public int Id { get; set; }

    [MaxLength(40)]
    public required string Nombre { get; set; }

    [MaxLength(255)]
    public string? LogoUrl { get; set; }

    [MaxLength(40)]
    public required string Colors { get; set; }

    [MaxLength(50)]
    public required string IdentificationId { get; set; }
    
    public required int PlanId { get; set; }

    public Plan? Plan { get; set; }

    /// <summary>
    /// The Vexxen user who pays for EvalFlow (the owner who created the company). Their subscription in Vexxen
    /// decides PlanId and PlanStatus. One company per subscription.
    /// </summary>
    [MaxLength(64)]
    public string? BillingVexxenUserId { get; set; }

    /// <summary>See <see cref="Constants.PlanStatuses"/>. Without access the company is read-only.</summary>
    [MaxLength(20)]
    public string PlanStatus { get; set; } = Constants.PlanStatuses.None;

    /// <summary>Until when the plan gives access if nothing changes (canceling, past_due). Null: no end date.</summary>
    public DateTime? PlanAccessEndsAt { get; set; }

    /// <summary>End of the free trial, if the subscription started with one.</summary>
    public DateTime? TrialEndsAt { get; set; }

    /// <summary>Last time the plan was read from Vexxen.</summary>
    public DateTime? PlanSyncedAt { get; set; }

    /// <summary>
    /// Plan set by hand by an Administrator (Enterprise, sold by the sales team): the sync with Vexxen leaves it alone.
    /// </summary>
    public bool PlanManual { get; set; }
    
    [MaxLength(15)]
    public required string Cif { get; set; }
    
    [MaxLength(150)]
    public string? DireccionFiscal { get; set; }

    [MaxLength(50)] public string Sector { get; set; } = "";
    
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    
    public DateTime? FechaActualizacion { get; set; }
    
    public ICollection<User> Usuarios { get; set; } = new List<User>();
}