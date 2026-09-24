using System.ComponentModel.DataAnnotations;
using evalflow_backend_api.Domain.Enums;

namespace evalflow_backend_api.Domain.Entities;

public class EmailOutboxMessage
{
    public int Id { get; set; }

    [MaxLength(320)]
    public required string To { get; set; }

    [MaxLength(300)]
    public required string Subject { get; set; }

    public required string EncryptedHtmlBody { get; set; }

    public EmailOutboxStatus Status { get; set; } = EmailOutboxStatus.Pendiente;

    public int Attempts { get; set; }

    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

    [MaxLength(2000)]
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }
}
