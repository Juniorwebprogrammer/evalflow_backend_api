using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace evalflow_backend_api.Infrastructure.Notifications;

public class BrevoSmtpEmailService(IConfiguration configuration) : IEmailService
{
    public async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var settings = configuration.GetSection("EmailSettings");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings["SenderName"], settings["SenderEmail"]!));
        message.To.Add(new MailboxAddress("", to));
        message.Subject = subject;
        message.Body = new TextPart(TextFormat.Html) { Text = htmlBody };

        using var client = new SmtpClient();
        
        // Nos conectamos de forma segura por el puerto 587 (StartTls)
        await client.ConnectAsync(settings["SmtpServer"]!, int.Parse(settings["SmtpPort"]!), SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(settings["SmtpUsername"]!, settings["SmtpPassword"]!, cancellationToken);
        
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}