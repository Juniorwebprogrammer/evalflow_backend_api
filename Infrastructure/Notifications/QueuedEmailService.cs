using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.Encryption;

namespace evalflow_backend_api.Infrastructure.Notifications;

public class QueuedEmailService(IServiceScopeFactory scopeFactory, IEncryptionService encryptionService, EmailQueueSignal signal)
    : IEmailService
{
    public async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            To = to,
            Subject = subject,
            EncryptedHtmlBody = encryptionService.Encrypt(htmlBody),
            CreatedAt = DateTime.UtcNow,
            NextAttemptAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        signal.Notify();
    }
}
