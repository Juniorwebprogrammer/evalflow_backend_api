namespace evalflow_backend_api.Infrastructure.Notifications;

public interface IEmailSender
{
    Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
