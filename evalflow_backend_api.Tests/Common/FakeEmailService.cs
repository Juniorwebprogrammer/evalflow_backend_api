using System.Collections.Concurrent;
using evalflow_backend_api.Infrastructure.Notifications;

namespace evalflow_backend_api.Tests.Common;

/// <summary>
/// Test double for IEmailService: never hits SMTP, just records what would have been sent
/// so tests can assert on it when needed.
/// </summary>
public class FakeEmailService : IEmailService
{
    public ConcurrentBag<SentEmail> SentEmails { get; } = new();

    public Task SendEmailAsync(string to, string subjets, string htmlBody, CancellationToken cancellationToken = default)
    {
        SentEmails.Add(new SentEmail(to, subjets, htmlBody));
        return Task.CompletedTask;
    }

    public record SentEmail(string To, string Subject, string HtmlBody);
}
