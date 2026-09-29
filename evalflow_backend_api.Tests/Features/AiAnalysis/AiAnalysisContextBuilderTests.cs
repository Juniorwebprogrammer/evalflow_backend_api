using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.AiAnalysis;

namespace evalflow_backend_api.Tests.Features.AiAnalysis;

public class AiAnalysisContextBuilderTests
{
    [Fact]
    public void Build_MasksNamesOfEvaluatedAndEvaluator_EvenInsideFreeText()
    {
        var comparison = AiAnalysisTestData.Employee(1, 1, questions:
            AiAnalysisTestData.Question(1, 5, 2, texto: "¿Enrique comunica sus avances?"));
        var clarification = new ClarificationForAnalysis(1, "Explicad la diferencia",
            "Marta nunca me dio feedback", "Enrique Pérez no entregó a tiempo", ClarificationStatus.Respondida);

        var json = AiAnalysisContextBuilder.Serialize(
            AiAnalysisContextBuilder.Build(EvaluationType.Evaluacion360, comparison, [clarification]));

        json.Should().NotContain("Enrique").And.NotContain("Pérez").And.NotContain("Marta").And.NotContain("López");
        json.Should().Contain("[evaluado] comunica").And.Contain("[evaluador] nunca").And.Contain("[evaluado] no entregó");
    }

    [Fact]
    public void Build_IncludesGapsLevelsAndClarifications()
    {
        var comparison = AiAnalysisTestData.Employee(1, 1);
        var clarification = new ClarificationForAnalysis(1, "¿Por qué?", "Respuesta A", null, ClarificationStatus.Parcial);

        var context = AiAnalysisContextBuilder.Build(EvaluationType.Evaluacion360, comparison, [clarification]);

        context.Preguntas.Should().HaveCount(2);
        context.Preguntas[0].Brecha.Should().Be(3);
        context.Preguntas[0].Nivel.Should().Be("Desequilibrio");
        context.Preguntas[0].Direccion.Should().Be("Sobrevaloracion");
        context.SolicitudesInformacion.Should().ContainSingle(c => c.RespuestaEvaluado == "Respuesta A" && c.Estado == "Parcial");
        context.Resumen!.Desequilibrios.Should().Be(1);
    }

    [Fact]
    public void Hash_IsStableForSameInputAndChangesWithData()
    {
        string HashOf(int self) => AiAnalysisContextBuilder.Hash(AiAnalysisContextBuilder.Serialize(
            AiAnalysisContextBuilder.Build(EvaluationType.Evaluacion360,
                AiAnalysisTestData.Employee(1, 1, questions: AiAnalysisTestData.Question(1, self, 2)), [])));

        HashOf(5).Should().Be(HashOf(5)).And.HaveLength(64);
        HashOf(4).Should().NotBe(HashOf(5));
    }
}
