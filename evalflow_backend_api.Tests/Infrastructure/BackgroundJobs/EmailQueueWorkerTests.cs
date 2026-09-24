using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Infrastructure.BackgroundJobs;
using evalflow_backend_api.Tests.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace evalflow_backend_api.Tests.Infrastructure.BackgroundJobs;

public class EmailQueueWorkerTests
{
    private static EmailQueueWorker CreateSut(EmailQueueTestHost host) =>
        new(host.Services.GetRequiredService<IServiceScopeFactory>(), host.Signal, NullLogger<EmailQueueWorker>.Instance);

    private static async Task<EmailOutboxMessage> EnqueueAsync(EmailQueueTestHost host, string to = "ana@example.com",
        int attempts = 0, DateTime? nextAttemptAt = null, EmailOutboxStatus status = EmailOutboxStatus.Pendiente)
    {
        await using var db = host.CreateDbContext();
        var message = new EmailOutboxMessage
        {
            To = to,
            Subject = "Asunto",
            EncryptedHtmlBody = "enc:<p>Hola</p>",
            Attempts = attempts,
            Status = status,
            NextAttemptAt = nextAttemptAt ?? DateTime.UtcNow.AddSeconds(-1),
        };
        db.EmailOutboxMessages.Add(message);
        await db.SaveChangesAsync();
        return message;
    }

    private static EmailOutboxMessage Reload(EmailQueueTestHost host, int id)
    {
        using var db = host.CreateDbContext();
        return db.EmailOutboxMessages.Single(m => m.Id == id);
    }

    [Fact]
    public async Task ProcessPendingAsync_SendsDecryptedBodyAndMarksAsSent()
    {
        using var host = new EmailQueueTestHost();
        var message = await EnqueueAsync(host);

        await CreateSut(host).ProcessPendingAsync(CancellationToken.None);

        host.Sender.Verify(s => s.SendEmailAsync("ana@example.com", "Asunto", "<p>Hola</p>", It.IsAny<CancellationToken>()), Times.Once);
        var stored = Reload(host, message.Id);
        stored.Status.Should().Be(EmailOutboxStatus.Enviado);
        stored.SentAt.Should().NotBeNull();
        stored.Attempts.Should().Be(1);
        stored.EncryptedHtmlBody.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenSenderFails_SchedulesRetry()
    {
        using var host = new EmailQueueTestHost();
        host.Sender
            .Setup(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("brevo down"));
        var message = await EnqueueAsync(host);

        await CreateSut(host).ProcessPendingAsync(CancellationToken.None);

        var stored = Reload(host, message.Id);
        stored.Status.Should().Be(EmailOutboxStatus.Pendiente);
        stored.Attempts.Should().Be(1);
        stored.LastError.Should().Be("brevo down");
        stored.NextAttemptAt.Should().BeAfter(DateTime.UtcNow);
        stored.EncryptedHtmlBody.Should().Be("enc:<p>Hola</p>");
    }

    [Fact]
    public async Task ProcessPendingAsync_OnLastAttempt_MarksAsFailedAndClearsBody()
    {
        using var host = new EmailQueueTestHost();
        host.Sender
            .Setup(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("brevo down"));
        var message = await EnqueueAsync(host, attempts: EmailQueueWorker.MaxAttempts - 1);

        await CreateSut(host).ProcessPendingAsync(CancellationToken.None);

        var stored = Reload(host, message.Id);
        stored.Status.Should().Be(EmailOutboxStatus.Fallido);
        stored.Attempts.Should().Be(EmailQueueWorker.MaxAttempts);
        stored.EncryptedHtmlBody.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessPendingAsync_SkipsMessagesNotDueOrAlreadyProcessed()
    {
        using var host = new EmailQueueTestHost();
        await EnqueueAsync(host, to: "later@example.com", nextAttemptAt: DateTime.UtcNow.AddMinutes(5));
        await EnqueueAsync(host, to: "sent@example.com", status: EmailOutboxStatus.Enviado);
        await EnqueueAsync(host, to: "failed@example.com", status: EmailOutboxStatus.Fallido);

        await CreateSut(host).ProcessPendingAsync(CancellationToken.None);

        host.Sender.Verify(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPendingAsync_ContinuesWithOtherMessagesWhenOneFails()
    {
        using var host = new EmailQueueTestHost();
        host.Sender
            .Setup(s => s.SendEmailAsync("bad@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("rejected"));
        var bad = await EnqueueAsync(host, to: "bad@example.com");
        var good = await EnqueueAsync(host, to: "good@example.com");

        await CreateSut(host).ProcessPendingAsync(CancellationToken.None);

        Reload(host, bad.Id).Status.Should().Be(EmailOutboxStatus.Pendiente);
        Reload(host, good.Id).Status.Should().Be(EmailOutboxStatus.Enviado);
    }

    [Fact]
    public async Task ProcessPendingAsync_DrainsMoreThanOneBatch()
    {
        using var host = new EmailQueueTestHost();
        for (var i = 0; i < 25; i++) await EnqueueAsync(host, to: $"user{i}@example.com");

        await CreateSut(host).ProcessPendingAsync(CancellationToken.None);

        host.Sender.Verify(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(25));
    }
}
