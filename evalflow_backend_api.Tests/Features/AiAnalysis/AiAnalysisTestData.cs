using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationComparisons.EvaluationComparisonsDto;

namespace evalflow_backend_api.Tests.Features.AiAnalysis;

internal static class AiAnalysisTestData
{
    public static QuestionComparisonDto Question(int id, int? self, int? manager, string topic = "Comunicación",
        string texto = "¿Comunica los avances al equipo?") => new(
        id, texto, QuestionType.Escala1a5, topic, id, self, manager, null, null,
        self - manager,
        self is null || manager is null ? AlignmentLevel.NoComparable
            : Math.Abs(self.Value - manager.Value) >= 2 ? AlignmentLevel.Desequilibrio
            : Math.Abs(self.Value - manager.Value) >= 1 ? AlignmentLevel.Leve : AlignmentLevel.Alineado,
        self > manager ? GapDirection.Sobrevaloracion : self < manager ? GapDirection.Infravaloracion : GapDirection.Ninguna,
        null);

    public static EmployeeComparisonDto Employee(int evaluatedUserId, int templateId, bool completed = true,
        string evaluatedName = "Enrique Pérez", string? managerName = "Marta López", params QuestionComparisonDto[] questions)
    {
        var list = questions.Length > 0 ? questions.ToList() : [Question(1, 5, 2), Question(2, 4, 4, "Calidad")];
        return new EmployeeComparisonDto(
            evaluatedUserId, evaluatedName, "Employee", templateId, "Desempeño", 99, managerName,
            completed, completed, completed,
            completed ? new ComparisonSummaryDto(list.Count, 1, 0, 1, 0, 50, 4.5, 3, 1.5, true) : null,
            completed ? [new TopicComparisonDto("Comunicación", 1, 5, 2, 3, AlignmentLevel.Desequilibrio, GapDirection.Sobrevaloracion)] : [],
            completed ? list : [],
            completed ? 1 : 0);
    }
}
