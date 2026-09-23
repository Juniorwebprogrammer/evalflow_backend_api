using System.Net;
using System.Text.Json;
using evalflow_backend_api.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;

namespace evalflow_backend_api.Tests.Infrastructure.Notifications;

public class BrevoApiEmailServiceTests
{
    private sealed class StubHandler(HttpStatusCode statusCode, string responseBody = "{\"messageId\":\"<id@brevo>\"}") : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) { Content = new StringContent(responseBody) };
        }
    }

    private static IConfiguration CreateConfiguration(string? apiKey = "xkeysib-test") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailSettings:ApiKey"] = apiKey,
                ["EmailSettings:SenderName"] = "EvalFlow",
                ["EmailSettings:SenderEmail"] = "no-reply@evalflow.test",
            })
            .Build();

    private static BrevoApiEmailService CreateSut(StubHandler handler, IConfiguration? configuration = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/v3/") }, configuration ?? CreateConfiguration());

    [Fact]
    public async Task SendEmailAsync_PostsTransactionalEmailToBrevoApi()
    {
        var handler = new StubHandler(HttpStatusCode.Created);

        await CreateSut(handler).SendEmailAsync("ana@example.com", "Código 2FA", "<p>123456</p>");

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri.Should().Be(new Uri("https://api.brevo.com/v3/smtp/email"));
        handler.Request.Headers.GetValues("api-key").Should().ContainSingle().Which.Should().Be("xkeysib-test");

        var body = JsonDocument.Parse(handler.Body!).RootElement;
        body.GetProperty("sender").GetProperty("email").GetString().Should().Be("no-reply@evalflow.test");
        body.GetProperty("sender").GetProperty("name").GetString().Should().Be("EvalFlow");
        body.GetProperty("to")[0].GetProperty("email").GetString().Should().Be("ana@example.com");
        body.GetProperty("subject").GetString().Should().Be("Código 2FA");
        body.GetProperty("htmlContent").GetString().Should().Be("<p>123456</p>");
    }

    [Fact]
    public async Task SendEmailAsync_WhenBrevoRejectsTheRequest_ThrowsWithBrevoDetail()
    {
        var handler = new StubHandler(HttpStatusCode.Unauthorized, "{\"code\":\"unauthorized\",\"message\":\"IP not authorized\"}");

        var act = () => CreateSut(handler).SendEmailAsync("ana@example.com", "Asunto", "<p>Hola</p>");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*401*IP not authorized*");
    }

    [Fact]
    public async Task SendEmailAsync_WithoutApiKey_ThrowsWithoutCallingBrevo()
    {
        var handler = new StubHandler(HttpStatusCode.Created);

        var act = () => CreateSut(handler, CreateConfiguration(apiKey: null)).SendEmailAsync("ana@example.com", "Asunto", "<p>Hola</p>");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*EmailSettings:ApiKey*");
        handler.Request.Should().BeNull();
    }
}
