using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Infrastructure.AI;

/// <summary>Calls <c>POST {BaseUrl}/chat/completions</c> of any OpenAI-compatible provider.</summary>
public class OpenAiCompatibleLlmClient(HttpClient httpClient, IOptions<AiSettings> options) : ILlmClient
{
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromMinutes(1);

    public async Task<LlmCompletion> CompleteJsonAsync(string systemPrompt, string userPrompt, string schemaName, object jsonSchema,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new LlmException("Las funciones de IA no están configuradas (Ai:Enabled / Ai:ApiKey).", false);

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(BuildPayload(settings, systemPrompt, userPrompt, schemaName, jsonSchema)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LlmException("El proveedor de IA no respondió a tiempo.", true);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmException($"No se pudo contactar con el proveedor de IA: {ex.Message}", true);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new LlmRateLimitedException("El proveedor de IA ha alcanzado su límite de uso.", GetRetryAfter(response));

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var transient = (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout;
                throw new LlmException($"El proveedor de IA respondió {(int)response.StatusCode}: {Truncate(body, 500)}", transient);
            }

            var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(body);
            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
                throw new LlmException("El proveedor de IA devolvió una respuesta vacía.", true);

            return new LlmCompletion(content, completion!.Model ?? settings.Model,
                completion.Usage?.PromptTokens, completion.Usage?.CompletionTokens);
        }
    }

    private static Dictionary<string, object> BuildPayload(AiSettings settings, string systemPrompt, string userPrompt,
        string schemaName, object jsonSchema)
    {
        object responseFormat = settings.ResponseFormat == "json_object"
            ? new { type = "json_object" }
            : new { type = "json_schema", json_schema = new { name = schemaName, schema = jsonSchema } };

        return new Dictionary<string, object>
        {
            ["model"] = settings.Model,
            ["temperature"] = settings.Temperature,
            ["max_tokens"] = settings.MaxTokens,
            ["response_format"] = responseFormat,
            ["messages"] = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
        };
    }

    private static TimeSpan GetRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta) return delta;
        if (retryAfter?.Date is { } date) return date - DateTimeOffset.UtcNow;
        return DefaultRetryAfter;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private record ChatCompletionResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] List<ChatChoice>? Choices,
        [property: JsonPropertyName("usage")] ChatUsage? Usage);

    private record ChatChoice([property: JsonPropertyName("message")] ChatMessage? Message);

    private record ChatMessage([property: JsonPropertyName("content")] string? Content);

    private record ChatUsage(
        [property: JsonPropertyName("prompt_tokens")] int? PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int? CompletionTokens);
}
