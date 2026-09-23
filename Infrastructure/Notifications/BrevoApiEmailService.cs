using System.Net.Http.Json;

namespace evalflow_backend_api.Infrastructure.Notifications;

public class BrevoApiEmailService(HttpClient httpClient, IConfiguration configuration) : IEmailService
{
    public async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(to, subject, htmlBody);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, to, cancellationToken);
    }

    private HttpRequestMessage BuildRequest(string to, string subject, string htmlBody)
    {
        var settings = configuration.GetSection("EmailSettings");

        var apiKey = settings["ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Falta la API key de Brevo en la configuración (EmailSettings:ApiKey).");

        var payload = new BrevoEmailRequest(
            new BrevoContact(settings["SenderEmail"]!, settings["SenderName"]),
            [new BrevoContact(to, null)],
            subject,
            htmlBody
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "smtp/email")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("api-key", apiKey);

        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string to, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);

        throw new InvalidOperationException(
            $"Brevo rechazó el email a {to} ({(int)response.StatusCode}): {detail}");
    }

    private record BrevoContact(string Email, string? Name);

    private record BrevoEmailRequest(BrevoContact Sender, List<BrevoContact> To, string Subject, string HtmlContent);
}
