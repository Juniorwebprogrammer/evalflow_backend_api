using System.Text;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationResults.DownloadEvaluationResultPdf;
using evalflow_backend_api.Features.EvaluationResults.EvaluationResultsDto;
using QuestPDF.Infrastructure;

namespace evalflow_backend_api.Tests.Features.EvaluationResults.DownloadEvaluationResultPdf;

public class EvaluationResultReportTests
{
    public EvaluationResultReportTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static ResultQuestionSnapshot Question(int id, string topic, QuestionType tipo, string? self, string? manager,
        string? final, AlignmentLevel level = AlignmentLevel.NoComparable, bool accepted = false) =>
        new(id, $"Pregunta {id}", tipo, topic, id, self, manager, final,
            final is null ? null : AcceptedAnswerSource.Superior, level, accepted);

    private static EvaluationResultSnapshot Snapshot(EvaluationType tipo, List<ResultQuestionSnapshot> questions) =>
        new("Acme Corp", "Q1 Ñandú", tipo, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow,
            "Desempeño", "Evaluación semestral", "Enrique User", "employee@example.com", "Comercial", "Ventas",
            tipo == EvaluationType.Auto ? null : "Marta User",
            tipo != EvaluationType.Evaluacion180, tipo != EvaluationType.Auto, false, DateTime.UtcNow,
            4, 3.5, 3.5, tipo == EvaluationType.Evaluacion360 ? 50 : null, questions);

    private static List<ResultQuestionSnapshot> MixedQuestions() =>
    [
        Question(1, "Liderazgo", QuestionType.Escala1a5, "5", "4", "4", AlignmentLevel.Leve),
        Question(2, "Liderazgo", QuestionType.Estrellas, "3", "3", "3", AlignmentLevel.Alineado),
        Question(3, "Comunicación", QuestionType.Escala1a5, "5", "1", "5", AlignmentLevel.Desequilibrio, accepted: true),
        Question(4, "Comunicación", QuestionType.Seleccion, "A, B", "A", "A", AlignmentLevel.Desequilibrio),
        Question(5, "Comunicación", QuestionType.Estrellas, null, null, null),
    ];

    [Fact]
    public void Topics_AverageOnlyNumericQuestions_PerRespondent()
    {
        var report = new EvaluationResultReport(Snapshot(EvaluationType.Evaluacion360, MixedQuestions()));

        report.Topics.Should().HaveCount(2);
        var leadership = report.Topics.Single(t => t.Topic == "Liderazgo");
        leadership.Self.Should().Be(4);
        leadership.Manager.Should().Be(3.5);
        leadership.Final.Should().Be(3.5);

        var communication = report.Topics.Single(t => t.Topic == "Comunicación");
        communication.QuestionCount.Should().Be(2); // the selection question is not scored
        communication.Final.Should().Be(5);
    }

    [Fact]
    public void Distribution_Highlights_AndCounts_AreDerivedFromFinalScores()
    {
        var report = new EvaluationResultReport(Snapshot(EvaluationType.Evaluacion360, MixedQuestions()));

        report.FinalDistribution.Should().Equal(0, 0, 1, 1, 1);
        report.Strengths.Select(q => q.Score).Should().Equal(5, 4);
        report.Improvements.Select(q => q.Score).Should().Equal(3);
        report.AnsweredCount.Should().Be(4);
        report.AcceptedCount.Should().Be(1);
        report.AlignmentCounts[AlignmentLevel.Desequilibrio].Should().Be(2);
    }

    [Theory]
    [InlineData(EvaluationType.Auto, true, false, false)]
    [InlineData(EvaluationType.Evaluacion180, false, true, false)]
    [InlineData(EvaluationType.Evaluacion360, true, true, true)]
    public void Columns_DependOnEvaluationType(EvaluationType tipo, bool self, bool manager, bool alignment)
    {
        var report = new EvaluationResultReport(Snapshot(tipo, MixedQuestions()));

        report.ShowSelf.Should().Be(self);
        report.ShowManager.Should().Be(manager);
        report.ShowAlignment.Should().Be(alignment);
        report.AlignmentCounts.Should().HaveCount(alignment ? 4 : 0);
    }

    [Theory]
    [InlineData(EvaluationType.Auto)]
    [InlineData(EvaluationType.Evaluacion180)]
    [InlineData(EvaluationType.Evaluacion360)]
    public void Generate_ProducesAPdf_ForEveryEvaluationType(EvaluationType tipo)
    {
        var pdf = EvaluationResultPdfDocument.Generate(Snapshot(tipo, MixedQuestions()));

        Encoding.ASCII.GetString(pdf[..4]).Should().Be("%PDF");
    }

    [Fact]
    public void Generate_ProducesAPdf_WhenThereAreNoNumericAnswers()
    {
        var questions = new List<ResultQuestionSnapshot>
        {
            Question(1, "General", QuestionType.Seleccion, "A", "B", "B", AlignmentLevel.Desequilibrio),
        };

        var pdf = EvaluationResultPdfDocument.Generate(Snapshot(EvaluationType.Evaluacion360, questions));

        Encoding.ASCII.GetString(pdf[..4]).Should().Be("%PDF");
    }
}
