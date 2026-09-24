using System.ComponentModel.DataAnnotations;
using evalflow_backend_api.Domain.Enums;

namespace evalflow_backend_api.Domain.Entities;

public class ClarificationRequest
{
    public int Id { get; set; }

    public int EvaluationCycleId { get; set; }
    public EvaluationCycle? Cycle { get; set; }

    public int TemplateId { get; set; }
    public Template? Template { get; set; }

    public int? QuestionId { get; set; }
    public Question? Question { get; set; }

    public int EvaluatedUserId { get; set; }
    public User? EvaluatedUser { get; set; }

    public int ManagerUserId { get; set; }
    public User? ManagerUser { get; set; }

    public int RequestedByUserId { get; set; }
    public User? RequestedByUser { get; set; }

    [MaxLength(1000)]
    public required string Mensaje { get; set; }

    public string? EvaluatedResponseEncrypted { get; set; }
    public DateTime? EvaluatedRespondedAt { get; set; }

    public string? ManagerResponseEncrypted { get; set; }
    public DateTime? ManagerRespondedAt { get; set; }

    public ClarificationStatus Estado { get; set; } = ClarificationStatus.Pendiente;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
