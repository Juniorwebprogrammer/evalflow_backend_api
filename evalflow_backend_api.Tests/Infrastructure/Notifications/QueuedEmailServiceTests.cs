using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace evalflow_backend_api.Tests.Infrastructure.Notifications;

public class QueuedEmailServiceTests
{
    [Fact]
    public async Task SendEmailAsync_StoresEncryptedMessageWithoutCallingTheSender()
    {
        using var host = new EmailQueueTestHost();
        var sut = new QueuedEmailService(host.Services.GetRequiredService<IServiceScopeFactory>(), host.Encryption.Object, host.Signal);

        await sut.SendEmailAsync("ana@example.com", "Código 2FA", "<p>123456</p>");

        await using var db = host.CreateDbContext();
        var message = db.EmailOutboxMessages.Should().ContainSingle().Subject;
        message.To.Should().Be("ana@example.com");
        message.Subject.Should().Be("Código 2FA");
        message.EncryptedHtmlBody.Should().Be("enc:<p>123456</p>");
        message.Status.Should().Be(EmailOutboxStatus.Pendiente);
        message.Attempts.Should().Be(0);
        host.Sender.Verify(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendEmailAsync_WakesUpTheWorker()
    {
        using var host = new EmailQueueTestHost();
        var sut = new QueuedEmailService(host.Services.GetRequiredService<IServiceScopeFactory>(), host.Encryption.Object, host.Signal);

        await sut.SendEmailAsync("ana@example.com", "Asunto", "<p>Hola</p>");

        var wait = host.Signal.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        (await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(1)))).Should().Be(wait);
    }
}
