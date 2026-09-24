using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Infrastructure.BackgroundJobs;

public class EmailQueueWorker(IServiceScopeFactory scopeFactory, EmailQueueSignal signal, ILogger<EmailQueueWorker> logger)
    : BackgroundService
{
    public const int MaxAttempts = 5;
    private const int BatchSize = 20;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30)
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "EmailQueueWorker: error al procesar la cola de emails.");
            }

            await signal.WaitAsync(PollInterval, stoppingToken);
        }
    }

    public async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        List<EmailOutboxMessage> batch;
        do
        {
            batch = await FetchDueBatchAsync(dbContext, cancellationToken);

            foreach (var message in batch)
            {
                await DeliverAsync(message, sender, encryptionService, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        } while (batch.Count == BatchSize);
    }

    private static async Task<List<EmailOutboxMessage>> FetchDueBatchAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        return await dbContext.EmailOutboxMessages
            .Where(m => m.Status == EmailOutboxStatus.Pendiente && m.NextAttemptAt <= now)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
    }

    private async Task DeliverAsync(EmailOutboxMessage message, IEmailSender sender, IEncryptionService encryptionService,
        CancellationToken cancellationToken)
    {
        try
        {
            var htmlBody = encryptionService.Decrypt(message.EncryptedHtmlBody);

            await sender.SendEmailAsync(message.To, message.Subject, htmlBody, cancellationToken);

            MarkAsSent(message);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            RegisterFailure(message, ex);
        }
    }

    private static void MarkAsSent(EmailOutboxMessage message)
    {
        message.Status = EmailOutboxStatus.Enviado;
        message.SentAt = DateTime.UtcNow;
        message.Attempts++;
        message.LastError = null;
        message.EncryptedHtmlBody = string.Empty;
    }

    private void RegisterFailure(EmailOutboxMessage message, Exception ex)
    {
        message.Attempts++;
        message.LastError = Truncate(ex.Message, 2000);

        if (message.Attempts >= MaxAttempts)
        {
            message.Status = EmailOutboxStatus.Fallido;
            message.EncryptedHtmlBody = string.Empty;
            logger.LogError(ex, "EmailQueueWorker: el email {MessageId} a {To} se descarta tras {Attempts} intentos.",
                message.Id, message.To, message.Attempts);
            return;
        }

        message.NextAttemptAt = DateTime.UtcNow.Add(RetryDelays[Math.Min(message.Attempts - 1, RetryDelays.Length - 1)]);
        logger.LogWarning(ex, "EmailQueueWorker: fallo al enviar el email {MessageId} a {To} (intento {Attempts}), se reintentará a las {NextAttemptAt}.",
            message.Id, message.To, message.Attempts, message.NextAttemptAt);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
