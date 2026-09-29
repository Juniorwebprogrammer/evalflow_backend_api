using System.Net;
using System.Text;
using System.Text.Json;
using evalflow_backend_api.Infrastructure.AI;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Tests.Infrastructure.AI;

public class OpenAiCompatibleLlmClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static readonly AiSettings Settings = new() { Enabled = true, ApiKey = "secret", Model = "openai/gpt-oss-120b" };

    private static (OpenAiCompatibleLlmClient Client, StubHandler Handler) CreateSut(HttpStatusCode status, string body,
        Action<HttpResponseMessage>? configure = null, AiSettings? settings = null)
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            configure?.Invoke(response);
            return response;
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.groq.com/openai/v1/") };
        return (new OpenAiCompatibleLlmClient(http, Options.Create(settings ?? Settings)), handler);
    }

    private static Task<LlmCompletion> Call(OpenAiCompatibleLlmClient client) =>
        client.CompleteJsonAsync("system", "user", "schema", new { type = "object" }, CancellationToken.None);

    [Fact]
    public async Task CompleteJsonAsync_SendsChatCompletionWithSchemaAndBearer()
    {
        var (client, handler) = CreateSut(HttpStatusCode.OK,
            """{"model":"openai/gpt-oss-120b","choices":[{"message":{"content":"{\"resumen\":\"x\"}"}}],"usage":{"prompt_tokens":10,"completion_tokens":5}}""");

        var result = await Call(client);

        result.Content.Should().Be("{\"resumen\":\"x\"}");
        result.PromptTokens.Should().Be(10);
        result.CompletionTokens.Should().Be(5);
        handler.Request!.RequestUri!.ToString().Should().Be("https://api.groq.com/openai/v1/chat/completions");
        handler.Request.Headers.Authorization!.ToString().Should().Be("Bearer secret");

        using var payload = JsonDocument.Parse(handler.Body!);
        payload.RootElement.GetProperty("model").GetString().Should().Be("openai/gpt-oss-120b");
        payload.RootElement.GetProperty("response_format").GetProperty("type").GetString().Should().Be("json_schema");
        payload.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("name").GetString().Should().Be("schema");
        payload.RootElement.GetProperty("messages").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task CompleteJsonAsync_JsonObjectMode_SendsJsonObjectFormat()
    {
        var settings = new AiSettings { Enabled = true, ApiKey = "k", ResponseFormat = "json_object" };
        var (client, handler) = CreateSut(HttpStatusCode.OK, """{"choices":[{"message":{"content":"{}"}}]}""", settings: settings);

        await Call(client);

        using var payload = JsonDocument.Parse(handler.Body!);
        payload.RootElement.GetProperty("response_format").GetProperty("type").GetString().Should().Be("json_object");
    }

    [Fact]
    public async Task CompleteJsonAsync_429_ThrowsRateLimitedWithRetryAfter()
    {
        var (client, _) = CreateSut(HttpStatusCode.TooManyRequests, "{}",
            r => r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(42)));

        var act = () => Call(client);

        (await act.Should().ThrowAsync<LlmRateLimitedException>()).Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(42));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    public async Task CompleteJsonAsync_ErrorStatus_ThrowsWithTransientFlag(HttpStatusCode status, bool transient)
    {
        var (client, _) = CreateSut(status, "{\"error\":\"x\"}");

        var act = () => Call(client);

        (await act.Should().ThrowAsync<LlmException>()).Which.IsTransient.Should().Be(transient);
    }

    [Fact]
    public async Task CompleteJsonAsync_Disabled_ThrowsNonTransient()
    {
        var (client, handler) = CreateSut(HttpStatusCode.OK, "{}", settings: new AiSettings { Enabled = false });

        var act = () => Call(client);

        (await act.Should().ThrowAsync<LlmException>()).Which.IsTransient.Should().BeFalse();
        handler.Request.Should().BeNull();
    }
}
