using System.Text.Json.Serialization;

namespace evalflow_backend_api.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<GapDirection>))]
public enum GapDirection
{
    Ninguna,
    Sobrevaloracion,
    Infravaloracion
}
