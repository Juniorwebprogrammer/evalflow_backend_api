using System.ComponentModel.DataAnnotations;

namespace evalflow_backend_api.Domain.Entities;

public class TemplateFavoriteList
{
    public int Id { get; set; }

    [MaxLength(100)]
    public required string Nombre { get; set; }

    [MaxLength(250)]
    public string? Descripcion { get; set; }

    public int UserId { get; set; }
    
    public User? Usuario { get; set; }

    public ICollection<Template> Templates { get; set; } = new List<Template>();
}