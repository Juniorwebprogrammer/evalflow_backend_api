namespace evalflow_backend_api.Domain.Entities;

public class Answer
{
    public int Id { get; set; }

    public int EvaluationSubmissionId { get; set; }
    public EvaluationSubmission? Submission { get; set; }

    public int QuestionId { get; set; }
    public Question? Question { get; set; }

    // Campos flexibles dependiendo del Tipo de Pregunta (QuestionType)
    public string? ValorTexto { get; set; } // Para tipo Desarrollo
    
    public int? ValorNumerico { get; set; } // Para tipo Estrellas (1-5) o Escala
    
    public List<string>? OpcionesSeleccionadas { get; set; } // Para tipo Selección
    
    public required string EncryptedPayload { get; set; }
}