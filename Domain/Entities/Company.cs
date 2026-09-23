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
    
    [MaxLength(15)]
    public required string Cif { get; set; }
    
    [MaxLength(150)]
    public string? DireccionFiscal { get; set; }

    [MaxLength(50)] public string Sector { get; set; } = "";
    
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    
    public DateTime? FechaActualizacion { get; set; }
    
    public ICollection<User> Usuarios { get; set; } = new List<User>();
}