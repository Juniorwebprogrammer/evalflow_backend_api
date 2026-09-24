using evalflow_backend_api.Domain.Enums;

namespace evalflow_backend_api.Domain.Entities;

public class DiscrepancyAcceptance
{
    public int Id { get; set; }

    public int EvaluationCycleId { get; set; }
    public EvaluationCycle? Cycle { get; set; }

    public int TemplateId { get; set; }
    public Template? Template { get; set; }

    public int EvaluatedUserId { get; set; }
    public User? EvaluatedUser { get; set; }

    public int QuestionId { get; set; }
    public Question? Question { get; set; }

    public AcceptedAnswerSource AcceptedSource { get; set; }

    public int AcceptedByUserId { get; set; }
    public User? AcceptedByUser { get; set; }

    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;
}
