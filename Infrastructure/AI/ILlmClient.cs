namespace evalflow_backend_api.Infrastructure.AI;

public record LlmCompletion(string Content, string Model, int? PromptTokens, int? CompletionTokens);

public interface ILlmClient
{
    /// <summary>Sends a system + user prompt and returns the model's JSON answer, matching <paramref name="jsonSchema"/>.</summary>
    Task<LlmCompletion> CompleteJsonAsync(string systemPrompt, string userPrompt, string schemaName, object jsonSchema,
        CancellationToken cancellationToken);
}

public class LlmException(string message, bool isTransient) : Exception(message)
{
    /// <summary>True when retrying later can succeed (timeouts, 5xx, rate limits).</summary>
    public bool IsTransient { get; } = isTransient;
}

/// <summary>The provider's quota was hit (HTTP 429); retry after <see cref="RetryAfter"/>.</summary>
public class LlmRateLimitedException(string message, TimeSpan retryAfter) : LlmException(message, true)
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}
