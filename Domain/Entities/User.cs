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

    [MaxLength(60)]
    public required string Email { get; set; }
    
    [MaxLength(255)]
    public required string PasswordHash { get; set; }
    
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
    
    public bool EmailVerificado { get; set; } = false;
    
    public string? TokenValidacionEmail { get; set; }

    public bool TwoFactorEnabled { get; set; } = false;
    
    public string? TwoFactorCode { get; set; }
    
    public string? TokenInvitacion { get; set; }
    
    [MaxLength(255)]
    public string? TokenRecuperacionPassword { get; set; }
    
    public DateTime? ExpiracionTokenRecuperacion { get; set; }
    
    public DateTime? TokenValidacionExpiracion { get; set; }
    
    public DateTime? TwoFactorCodeExpiration { get; set; }
    
    public DateTime? TokenInvitacionExpiracion { get; set; }
    
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    
    public DateTime? FechaActualizacion { get; set; }
    
    public Company? Empresa { get; set; }
    
    public Department? Departamento { get; set; }
    
    public User? Superior { get; set; }
    
    public JobPosition? Cargo { get; set; }
}