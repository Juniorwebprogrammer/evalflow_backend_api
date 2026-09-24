using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace evalflow_backend_api.Tests.Infrastructure.Notifications;

public sealed class EmailQueueTestHost : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly InMemoryDatabaseRoot _root = new();

    public Mock<IEmailSender> Sender { get; } = new();
    public Mock<IEncryptionService> Encryption { get; } = new();
    public EmailQueueSignal Signal { get; } = new();
    public ServiceProvider Services { get; }

    public EmailQueueTestHost()
    {
        Encryption.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(plain => $"enc:{plain}");
        Encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(cipher => cipher[4..]);

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName, _root));
        services.AddSingleton(Sender.Object);
        services.AddSingleton(Encryption.Object);
        services.AddSingleton(Signal);
        Services = services.BuildServiceProvider();
    }

    public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName, _root).Options);

    public void Dispose() => Services.Dispose();
}
