using System.ComponentModel.DataAnnotations;

namespace evalflow_backend_api.Domain.Entities;

public class Department
{
    public int Id { get; set; }

    [MaxLength(60)]
    public required string Nombre { get; set; }

    [MaxLength(200)]
    public string? Descripcion { get; set; }

    public int EmpresaID { get; set; }
    
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Relaciones
    public Company? Empresa { get; set; }
    public ICollection<User> Usuarios { get; set; } = new List<User>();
}