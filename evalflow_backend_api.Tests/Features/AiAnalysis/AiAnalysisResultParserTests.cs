using evalflow_backend_api.Features.AiAnalysis;
using evalflow_backend_api.Infrastructure.AI;

namespace evalflow_backend_api.Tests.Features.AiAnalysis;

public class AiAnalysisResultParserTests
{
    [Fact]
    public void Parse_ValidJson_MapsEveryField()
    {
        const string json = """
            {"resumen":"El evaluado se sobrevalora en comunicación.","nivelRiesgo":"Alto",
             "fortalezas":["Calidad"],"areasDeMejora":["Comunicación"],
             "causasProbables":[{"tema":"Comunicación","preguntaIds":[1],"causa":"Falta de feedback",
               "explicacion":"No recibe feedback.","evidencia":["Respuesta del evaluado"],"confianza":"Alta"}],
             "patrones":["Sobrevaloración"],"recomendaciones":[{"destinatario":"Evaluador","accion":"Reuniones 1:1"}],
             "solicitudesSugeridas":[{"preguntaId":1,"mensaje":"¿Qué ejemplos tienes?"}],"limitaciones":[]}
            """;

        var result = AiAnalysisResultParser.Parse(json);

        result.NivelRiesgo.Should().Be("Alto");
        result.CausasProbables.Should().ContainSingle(c => c.Causa == "Falta de feedback" && c.PreguntaIds.Contains(1));
        result.Recomendaciones.Should().ContainSingle(r => r.Destinatario == "Evaluador");
        result.SolicitudesSugeridas.Should().ContainSingle(s => s.PreguntaId == 1);
    }

    [Fact]
    public void Parse_CodeFenceAndUnknownValues_AreNormalised()
    {
        const string json = """
            ```json
            {"resumen":"Ok","nivelRiesgo":"altísimo","causasProbables":[{"tema":"X","causa":"inventada","explicacion":"e","confianza":"?"}],
             "recomendaciones":[{"destinatario":"CEO","accion":"a"}]}
            ```
            """;

        var result = AiAnalysisResultParser.Parse(json);

        result.NivelRiesgo.Should().Be("Medio");
        result.CausasProbables.Single().Causa.Should().Be("Otro");
        result.CausasProbables.Single().Confianza.Should().Be("Media");
        result.Recomendaciones.Single().Destinatario.Should().Be("RRHH");
        result.Fortalezas.Should().BeEmpty();
        result.Limitaciones.Should().BeEmpty();
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("{\"nivelRiesgo\":\"Alto\"}")]
    public void Parse_InvalidOrWithoutSummary_ThrowsTransientLlmException(string content)
    {
        var act = () => AiAnalysisResultParser.Parse(content);

        act.Should().Throw<LlmException>().Which.IsTransient.Should().BeTrue();
    }
}
