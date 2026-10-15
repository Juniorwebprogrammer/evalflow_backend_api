using System.ComponentModel.DataAnnotations;
using evalflow_backend_api.Domain.Constants;

namespace evalflow_backend_api.Domain.Entities;

public class User
{
    public int Id { get; set; }

    [MaxLength(40)]
    public required string Nombre { get; set; }

    [MaxLength(40)]
    public required string Apellidos { get; set; }

    /// <summary>Copied from the Vexxen account on every sign-in (Vexxen owns the email and the password).</summary>
    [MaxLength(60)]
    public required string Email { get; set; }

    /// <summary>
    /// The Vexxen account this user signs in with. Null while an invitation is pending: it gets linked the first
    /// time the invited person signs in with Vexxen using the invited email.
    /// </summary>
    [MaxLength(64)]
    public string? VexxenUserId { get; set; }
    
    public int EmpresaID { get; set; }
    
    public int? DepartamentoId { get; set; }
    
    public int? SuperiorId { get; set; }
    
    public string Rol { get; set; } = AppRoles.Employee;

    public bool Activo { get; set; } = true;
    
    public int? CargoId { get; set; }

    public ICollection<User> Subordinados { get; set; } = [];

    public ICollection<Template> TemplatesAsignados { get; set; } = new List<Template>();

    public ICollection<TemplateFavoriteList> ListasFavoritas { get; set; } = new List<TemplateFavoriteList>();

    public ICollection<EvaluationSubmission> EvaluacionesRecibidas { get; set; } = new List<EvaluationSubmission>();

    public ICollection<EvaluationSubmission> EvaluacionesRealizadas { get; set; } = new List<EvaluationSubmission>();
    
    /// <summary>Pending invitation (link in the email). Cleared when the invited person signs in for the first time.</summary>
    public string? TokenInvitacion { get; set; }

    public DateTime? TokenInvitacionExpiracion { get; set; }
    
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    
    public DateTime? FechaActualizacion { get; set; }
    
    public Company? Empresa { get; set; }
    
    public Department? Departamento { get; set; }
    
    public User? Superior { get; set; }
    
    public JobPosition? Cargo { get; set; }
}