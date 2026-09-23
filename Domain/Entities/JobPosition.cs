using System.ComponentModel.DataAnnotations;

namespace evalflow_backend_api.Domain.Entities;

public class JobPosition
{
    public int Id { get; set; }

    [MaxLength(100)]
    public required string Nombre { get; set; }

    [MaxLength(250)]
    public string? Descripcion { get; set; }

    public int EmpresaID { get; set; }
    public Company? Empresa { get; set; }

    public ICollection<User> Usuarios { get; set; } = new List<User>();
}