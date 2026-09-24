namespace evalflow_backend_api.Domain.Entities;

public class EvaluationResult
{
    public int Id { get; set; }

    public int EvaluationCycleId { get; set; }
    public EvaluationCycle? Cycle { get; set; }

    public int TemplateId { get; set; }
    public Template? Template { get; set; }

    public int EvaluatedUserId { get; set; }
    public User? EvaluatedUser { get; set; }

    public int CompletedByUserId { get; set; }
    public User? CompletedByUser { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public double? AverageFinal { get; set; }

    public required string EncryptedSnapshot { get; set; }
}
