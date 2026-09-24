using System.Text.Json.Serialization;

namespace evalflow_backend_api.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<ClarificationStatus>))]
public enum ClarificationStatus
{
    Pendiente,
    Parcial,
    Respondida
}
