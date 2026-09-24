namespace evalflow_backend_api.Infrastructure.Frontend;

public class FrontendSettings
{
    public const string SectionName = "Frontend";

    public string BaseUrl { get; set; } = string.Empty;

    public string BuildLink(string relativePath) => $"{BaseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
}
