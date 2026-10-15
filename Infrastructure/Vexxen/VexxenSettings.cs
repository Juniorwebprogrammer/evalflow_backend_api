namespace evalflow_backend_api.Infrastructure.Vexxen;

/// <summary>
/// Conexión con Vexxen (cuentas, sesiones y planes). El cliente es el mismo que usa el popup del frontend
/// (NEXT_PUBLIC_VEXXEN_CLIENT_ID): los refresh tokens de Vexxen van ligados a él.
/// Variables de entorno: Vexxen__BaseUrl, Vexxen__ClientId, Vexxen__ApiKey (secreta).
/// </summary>
public class VexxenSettings
{
    public const string SectionName = "Vexxen";

    public string BaseUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ApiKey { get; set; } = "";

    /// <summary>El producto de EvalFlow en el catálogo de Vexxen.</summary>
    public string ProductId { get; set; } = "evaluation-flow";

    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Donde el usuario elige plan y empieza la prueba (el catálogo de Vexxen, con EvalFlow abierto).</summary>
    public string CatalogUrl => $"{BaseUrl.TrimEnd('/')}/dashboard/catalog?product={Uri.EscapeDataString(ProductId)}";

    public bool IsValid() =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ApiKey);
}
