using System.ComponentModel.DataAnnotations;
using evalflow_backend_api.Domain.Enums;

namespace evalflow_backend_api.Domain.Entities;

/// <summary>
/// An AI-generated explanation of one employee's evaluation (per cycle and template):
/// why self and manager answers diverge, strengths, areas to improve and next steps.
/// Requested by Owner/Rrhh and processed in the background by <c>AiAnalysisWorker</c>.
/// </summary>
public class AiEvaluationAnalysis
{
    public int Id { get; set; }

    public int EmpresaID { get; set; }
    public Company? Empresa { get; set; }

    public int EvaluationCycleId { get; set; }
    public EvaluationCycle? Cycle { get; set; }

    public int TemplateId { get; set; }
    public Template? Template { get; set; }

    public int EvaluatedUserId { get; set; }
    public User? EvaluatedUser { get; set; }

    public int RequestedByUserId { get; set; }
    public User? RequestedByUser { get; set; }

    public AiAnalysisStatus Estado { get; set; } = AiAnalysisStatus.Pendiente;

    /// <summary>SHA-256 of the pseudonymised input, so an unchanged evaluation reuses its analysis.</summary>
    [MaxLength(64)]
    public required string InputHash { get; set; }

    /// <summary>Encrypted input sent to the model; cleared once the analysis is done.</summary>
    public string InputEncrypted { get; set; } = string.Empty;

    /// <summary>Encrypted JSON of the model's answer (<c>AiAnalysisResultDto</c>).</summary>
    public string? ResultEncrypted { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }

    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

    [MaxLength(2000)]
    public string? ErrorMensaje { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaCompletado { get; set; }
}
