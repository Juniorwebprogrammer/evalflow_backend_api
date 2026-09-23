using System.ComponentModel.DataAnnotations;
using evalflow_backend_api.Domain.Enums;

namespace evalflow_backend_api.Domain.Entities;

public class Question
{
    public int Id { get; set; }
    [MaxLength(500)]
    public required string Texto { get; set; }
    public QuestionType Tipo { get; set; } 
    public string Topic { get; set; } = "General";
    public List<string>? Opciones { get; set; } 
    public int Orden { get; set; } 
    public int TemplateId { get; set; }
    public Template? Template { get; set; }
}