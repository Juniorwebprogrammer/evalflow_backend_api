using System.Text.Json.Serialization;

namespace evalflow_backend_api.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<AiAnalysisStatus>))]
public enum AiAnalysisStatus
{
    Pendiente,
    Procesando,
    Completado,
    Error
}
