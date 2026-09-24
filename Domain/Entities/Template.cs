using System.ComponentModel.DataAnnotations;

namespace evalflow_backend_api.Domain.Entities;

public class Template
{
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Titulo { get; set; }

    public string? Descripcion { get; set; }

    public DateTime FechaInicio { get; set; }
    
    public DateTime FechaFin { get; set; }
    
    public int EmpresaID { get; set; }
    
    public Company? Empresa { get; set; }
    
    public ICollection<User> UsuariosAsignados { get; set; } = new List<User>();

    public ICollection<TemplateFavoriteList> ListasFavoritas { get; set; } = new List<TemplateFavoriteList>();

    public ICollection<EvaluationCycle> Ciclos { get; set; } = new List<EvaluationCycle>();

    public ICollection<Question> Preguntas { get; set; } = new List<Question>();
}