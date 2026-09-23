using System.Text.Json.Serialization;

namespace evalflow_backend_api.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<AlignmentLevel>))]
public enum AlignmentLevel
{
    Alineado,
    Leve,
    Desequilibrio,
    NoComparable
}
