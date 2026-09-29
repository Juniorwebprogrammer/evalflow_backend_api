namespace evalflow_backend_api.Infrastructure.AI;

/// <summary>
/// Any OpenAI-compatible chat completions API (Groq, Gemini, Mistral, OpenRouter…).
/// Switching provider is a configuration change: BaseUrl + Model + ApiKey.
/// </summary>
public class AiSettings
{
    public const string SectionName = "Ai";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "openai/gpt-oss-120b";

    public int MaxTokens { get; set; } = 3000;

    public double Temperature { get; set; } = 0.2;

    public int TimeoutSeconds { get; set; } = 90;

    /// <summary>"json_schema" (strict output) or "json_object" for providers without schema support.</summary>
    public string ResponseFormat { get; set; } = "json_schema";

    /// <summary>Global cap across every company, kept under the provider's free daily quota.</summary>
    public int MaxRequestsPerDay { get; set; } = 800;

    /// <summary>Pause between calls so a free tokens-per-minute quota isn't exceeded.</summary>
    public int MinSecondsBetweenRequests { get; set; } = 10;

    public bool IsValid() =>
        !Enabled || (!string.IsNullOrWhiteSpace(ApiKey)
                     && Uri.TryCreate(BaseUrl, UriKind.Absolute, out _)
                     && !string.IsNullOrWhiteSpace(Model));
}
