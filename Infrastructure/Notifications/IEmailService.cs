namespace evalflow_backend_api.Infrastructure.Notifications;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subjets, string htmlBody, CancellationToken cancellationToken = default);
}