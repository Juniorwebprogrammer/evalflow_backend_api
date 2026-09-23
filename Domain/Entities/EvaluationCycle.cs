using System.ComponentModel.DataAnnotations;
using evalflow_backend_api.Domain.Enums;

namespace evalflow_backend_api.Domain.Entities;

public class EvaluationCycle
{
    public int Id { get; set; }

    [MaxLength(100)]
    public required string Nombre { get; set; }

    [MaxLength(250)]
    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaInicio { get; set; }
    
    public DateTime FechaFin { get; set; }

    public int EmpresaID { get; set; }
    
    public Company? Empresa { get; set; }

    public EvaluationType TipoEvaluación { get; set; } = EvaluationType.Auto;
    
    public ICollection<Template> Templates { get; set; } = new List<Template>();
}