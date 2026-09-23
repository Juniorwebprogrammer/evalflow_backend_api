namespace evalflow_backend_api.Domain.Entities;

public class EvaluationSubmission
{
    public int Id { get; set; }

    // Contexto de la evaluación
    public int EvaluationCycleId { get; set; }
    public EvaluationCycle? Cycle { get; set; }

    public int TemplateId { get; set; }
    public Template? Template { get; set; }

    // El empleado que está siendo evaluado
    public int EvaluatedUserId { get; set; }
    public User? EvaluatedUser { get; set; }

    // La persona que está escribiendo las respuestas
    public int RespondentUserId { get; set; }
    public User? RespondentUser { get; set; }

    // Estado del formulario
    public bool IsCompleted { get; set; } = false;
    public DateTime? SubmittedAt { get; set; }

    // Relación con las respuestas
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    
    // Control de notificaciones
    public bool ReminderSent { get; set; } = false;
}