using System.Text.Json.Serialization;

namespace evalflow_backend_api.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<ClarificationParticipant>))]
public enum ClarificationParticipant
{
    Evaluado,
    Evaluador
}
