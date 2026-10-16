using System.Linq.Expressions;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.AdminApi.Contract;
using evalflow_backend_api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.AdminApi.Resources;

public sealed record EmailOutboxAdminItem(
    string Id,
    string CreatedAt,
    string To,
    string Subject,
    string Status,
    int Attempts,
    string? LastError,
    string NextAttemptAt,
    string? SentAt);

/// <summary>
/// The email queue (EmailQueueWorker sends it through Brevo): to see what failed or is still waiting. The body is
/// encrypted and is never returned.
/// </summary>
public class EmailOutboxAdminResource(AppDbContext dbContext) : IAdminResource
{
    public const string Key = "email-outbox";

    public static readonly IReadOnlyList<EnumOption> StatusOptions =
    [
        new("pending", "Pendiente", "warning"),
        new("sent", "Enviado", "success"),
        new("failed", "Fallido", "danger"),
    ];

    public static readonly ResourceDescriptor Definition = new(
        Key,
        "Emails",
        "Email",
        "Sistema",
        "La cola de envío de emails (invitaciones, evaluaciones, resultados…).",
        [
            new("createdAt", "Creado", FieldTypes.DateTime),
            new("to", "Para", FieldTypes.Email),
            new("subject", "Asunto", FieldTypes.String),
            new("status", "Estado", FieldTypes.Enum) { Enum = StatusOptions },
            new("attempts", "Intentos", FieldTypes.Number),
            new("lastError", "Último error", FieldTypes.Text) { Nullable = true },
            new("nextAttemptAt", "Próximo intento", FieldTypes.DateTime),
            new("sentAt", "Enviado", FieldTypes.DateTime) { Nullable = true },
        ],
        new ListDescriptor(
            ["createdAt", "to", "subject", "status", "attempts"],
            "-createdAt",
            ["createdAt", "attempts"],
            new SearchDescriptor("Destinatario o asunto"),
            [
                new("status", "Estado", FilterTypes.Enum) { Multiple = true },
                new("createdAt", "Creado", FilterTypes.DateRange),
            ]),
        true,
        [],
        []);

    public ResourceDescriptor Descriptor => Definition;

    public static string StatusValue(EmailOutboxStatus status) => status switch
    {
        EmailOutboxStatus.Pendiente => "pending",
        EmailOutboxStatus.Enviado => "sent",
        EmailOutboxStatus.Fallido => "failed",
        _ => status.ToString().ToLowerInvariant(),
    };

    private static EmailOutboxStatus StatusFromValue(string value) => value switch
    {
        "pending" => EmailOutboxStatus.Pendiente,
        "sent" => EmailOutboxStatus.Enviado,
        _ => EmailOutboxStatus.Fallido,
    };

    private static readonly Expression<Func<EmailOutboxMessage, EmailOutboxAdminItem>> ToItem = m => new EmailOutboxAdminItem(
        m.Id.ToString(),
        AdminFormat.DateTime(m.CreatedAt),
        m.To,
        m.Subject,
        StatusValue(m.Status),
        m.Attempts,
        m.LastError,
        AdminFormat.DateTime(m.NextAttemptAt),
        AdminFormat.DateTime(m.SentAt));

    public async Task<AdminListResult> ListAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var messages = dbContext.EmailOutboxMessages.AsNoTracking();

        if (query.Q is { } q)
        {
            var term = q.ToLowerInvariant();
            messages = messages.Where(m => m.To.ToLower().Contains(term) || m.Subject.ToLower().Contains(term));
        }
        if (query.Filter<EnumFilterValue>("status") is { } status)
        {
            var statuses = status.Values.Select(StatusFromValue).ToList();
            messages = messages.Where(m => statuses.Contains(m.Status));
        }
        if (query.Filter<DateRangeFilterValue>("createdAt") is { } createdAt)
        {
            if (createdAt.From is { } from) messages = messages.Where(m => m.CreatedAt >= from);
            if (createdAt.To is { } to) messages = messages.Where(m => m.CreatedAt <= to);
        }

        var total = await messages.CountAsync(cancellationToken);
        var sorted = (query.Sort.Key, query.Sort.Descending) switch
        {
            ("attempts", false) => messages.OrderBy(m => m.Attempts).ThenBy(m => m.Id),
            ("attempts", true) => messages.OrderByDescending(m => m.Attempts).ThenByDescending(m => m.Id),
            (_, false) => messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id),
            (_, true) => messages.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id),
        };
        var items = await sorted.Page(query).Select(ToItem).ToListAsync(cancellationToken);
        return new AdminListResult(items, total);
    }

    public async Task<object?> GetAsync(string id, CancellationToken cancellationToken)
    {
        if (AdminQueryExtensions.ParseId(id) is not { } messageId) return null;
        return await dbContext.EmailOutboxMessages.AsNoTracking().Where(m => m.Id == messageId).Select(ToItem).FirstOrDefaultAsync(cancellationToken);
    }
}
