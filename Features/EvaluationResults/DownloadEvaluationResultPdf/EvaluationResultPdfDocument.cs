using System.Globalization;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationResults.EvaluationResultsDto;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static evalflow_backend_api.Features.EvaluationResults.DownloadEvaluationResultPdf.EvaluationResultPdfCharts;

namespace evalflow_backend_api.Features.EvaluationResults.DownloadEvaluationResultPdf;

/// <summary>
/// Lays out the evaluation report: summary figures, charts per topic and
/// alignment, strengths / improvement areas, then every question grouped by
/// topic. All numbers come from <see cref="EvaluationResultReport"/>.
/// </summary>
public static class EvaluationResultPdfDocument
{
    private const string BrandColor = "#2563eb";
    private const string Surface = "#f8fafc";
    private const string Border = "#e2e8f0";
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    public static byte[] Generate(EvaluationResultSnapshot snapshot)
    {
        var report = new EvaluationResultReport(snapshot);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style.FontSize(9).FontColor("#334155"));

                page.Header().Element(header => ComposeHeader(header, snapshot));
                page.Content().PaddingVertical(14).Element(content => ComposeContent(content, report));
                page.Footer().Element(footer => ComposeFooter(footer, snapshot));
            });
        })
        .WithMetadata(new DocumentMetadata
        {
            Title = $"Informe de evaluación · {snapshot.EvaluatedUserName}",
            Author = snapshot.CompanyName,
            Subject = $"{snapshot.CycleName} · {snapshot.TemplateTitle}",
            Creator = "EvalFlow",
        })
        .GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, EvaluationResultSnapshot snapshot)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text(text =>
                {
                    text.Span("Eval").FontSize(16).Bold().FontColor(Ink);
                    text.Span("Flow").FontSize(16).Bold().FontColor(BrandColor);
                    text.Span("   Informe de resultados de evaluación").FontSize(9).FontColor(Muted);
                });
                row.ConstantItem(200).AlignRight().AlignMiddle().Column(meta =>
                {
                    meta.Item().AlignRight().Text(snapshot.CompanyName).SemiBold().FontColor(Ink);
                    meta.Item().AlignRight().Text($"Ciclo cerrado el {FormatDate(snapshot.CompletedAt)}")
                        .FontSize(8).FontColor(Muted);
                });
            });
            column.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(BrandColor);
        });
    }

    private static void ComposeContent(IContainer container, EvaluationResultReport report)
    {
        container.Column(column =>
        {
            column.Spacing(18);
            column.Item().Element(c => ComposeTitle(c, report.Snapshot));
            column.Item().Element(c => ComposeDetails(c, report.Snapshot));
            column.Item().Element(c => ComposeFigures(c, report));

            if (report.Topics.Count > 0)
                column.Item().Element(c => ComposeTopics(c, report));

            column.Item().ShowEntire().Element(c => ComposeBreakdown(c, report));

            // ShowEntire: move the block to the next page rather than orphan its titles.
            if (report.Strengths.Count > 0 || report.Improvements.Count > 0)
                column.Item().ShowEntire().Element(c => ComposeHighlights(c, report));

            column.Item().Element(c => ComposeQuestions(c, report));
            column.Item().Element(c => ComposeNotes(c, report));
        });
    }

    private static void ComposeTitle(IContainer container, EvaluationResultSnapshot snapshot)
    {
        container.Column(column =>
        {
            column.Item().Text(snapshot.EvaluatedUserName).FontSize(20).Bold().FontColor(Ink);
            column.Item().PaddingTop(2).Text(text =>
            {
                text.Span(snapshot.TemplateTitle).SemiBold().FontColor(BrandColor);
                text.Span($"  ·  {snapshot.CycleName}  ·  {EvaluationTypeLabel(snapshot.TipoEvaluacion)}").FontColor(Muted);
            });
            if (!string.IsNullOrWhiteSpace(snapshot.TemplateDescription))
                column.Item().PaddingTop(4).Text(snapshot.TemplateDescription).FontSize(8.5f).Italic().FontColor(Muted);
        });
    }

    private static void ComposeDetails(IContainer container, EvaluationResultSnapshot snapshot)
    {
        container.Background(Surface).Border(1).BorderColor(Border).CornerRadius(6).Padding(12).Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Spacing(4);
                DetailLine(left, "Empleado", snapshot.EvaluatedUserName);
                DetailLine(left, "Email", snapshot.EvaluatedUserEmail);
                DetailLine(left, "Puesto", snapshot.Cargo ?? "—");
                DetailLine(left, "Departamento", snapshot.Departamento ?? "—");
            });
            row.ConstantItem(16);
            row.RelativeItem().Column(right =>
            {
                right.Spacing(4);
                DetailLine(right, "Evaluador", snapshot.ManagerName ?? "—");
                DetailLine(right, "Periodo", $"{FormatDate(snapshot.FechaInicio)} – {FormatDate(snapshot.FechaFin)}");
                DetailLine(right, "Autoevaluación", ParticipationLabel(snapshot.TipoEvaluacion != EvaluationType.Evaluacion180, snapshot.SelfCompleted));
                DetailLine(right, "Evaluación del superior", ParticipationLabel(snapshot.TipoEvaluacion != EvaluationType.Auto, snapshot.ManagerCompleted));
            });
        });
    }

    /// <summary>Headline figures: final score first, then each respondent and the alignment.</summary>
    private static void ComposeFigures(IContainer container, EvaluationResultReport report)
    {
        var s = report.Snapshot;
        var total = s.Questions.Count;

        container.Row(row =>
        {
            row.Spacing(10);
            row.RelativeItem(1.3f).Element(c => ScoreTile(c, "Puntuación final", s.AverageFinal, BrandColor, highlight: true));
            if (report.ShowSelf)
                row.RelativeItem().Element(c => ScoreTile(c, "Autoevaluación", s.AverageSelf, SeriesSelf));
            if (report.ShowManager)
                row.RelativeItem().Element(c => ScoreTile(c, "Evaluación del superior", s.AverageManager, SeriesManager));
            if (report.ShowAlignment)
                row.RelativeItem().Element(c => RatioTile(c, "Alineación", s.AlignmentPercentage, "coincidencia entre ambas"));
            row.RelativeItem().Element(c => RatioTile(c, "Preguntas respondidas",
                total == 0 ? null : report.AnsweredCount * 100.0 / total, $"{report.AnsweredCount} de {total}"));
        });
    }

    private static void ScoreTile(IContainer container, string label, double? score, string color, bool highlight = false)
    {
        Tile(container, highlight).Column(column =>
        {
            column.Item().Text(label).FontSize(8).FontColor(Muted);
            column.Item().PaddingTop(3).Text(text =>
            {
                text.Span(score is null ? "—" : FormatScore(score.Value)).FontSize(highlight ? 22 : 17).Bold().FontColor(Ink);
                if (score is not null) text.Span($" / {EvaluationResultReport.MaxScore}").FontSize(9).FontColor(Muted);
            });
            column.Item().PaddingTop(6).Height(5).Svg(size => Meter(score ?? 0, EvaluationResultReport.MaxScore, size.Width, size.Height, color));
        });
    }

    private static void RatioTile(IContainer container, string label, double? percentage, string caption)
    {
        Tile(container).Column(column =>
        {
            column.Item().Text(label).FontSize(8).FontColor(Muted);
            column.Item().PaddingTop(3).Text(percentage is null ? "—" : $"{Math.Round(percentage.Value):0}%").FontSize(17).Bold().FontColor(Ink);
            column.Item().PaddingTop(6).Height(5).Svg(size => Meter(percentage ?? 0, 100, size.Width, size.Height, "#64748b"));
            column.Item().PaddingTop(3).Text(caption).FontSize(7).FontColor(Muted);
        });
    }

    private static IContainer Tile(IContainer container, bool highlight = false) =>
        container
            .Background(highlight ? "#eff6ff" : Colors.White)
            .Border(1).BorderColor(highlight ? "#bfdbfe" : Border)
            .CornerRadius(6)
            .Padding(10);

    private static void ComposeTopics(IContainer container, EvaluationResultReport report)
    {
        var twoSeries = report.ShowSelf && report.ShowManager;

        container.Column(column =>
        {
            SectionTitle(column, "Resultados por competencia", "Media de cada competencia sobre 5 puntos");

            if (twoSeries)
            {
                column.Item().PaddingBottom(6).Row(legend =>
                {
                    legend.Spacing(14);
                    legend.AutoItem().Element(c => LegendItem(c, SeriesSelf, "Autoevaluación"));
                    legend.AutoItem().Element(c => LegendItem(c, SeriesManager, "Superior"));
                });
            }

            column.Item()
                .Height(TopicBarsHeight(report.Topics.Count, twoSeries))
                .Svg(size => TopicBars(report.Topics, report.ShowSelf, report.ShowManager, size.Width));
        });
    }

    /// <summary>Distribution of final scores, next to the alignment donut (360°) or the participation summary.</summary>
    private static void ComposeBreakdown(IContainer container, EvaluationResultReport report)
    {
        var hasScores = report.FinalDistribution.Any(c => c > 0);
        if (!hasScores && !report.ShowAlignment) return;

        container.Row(row =>
        {
            row.Spacing(16);

            if (hasScores)
            {
                row.RelativeItem().Element(card => Tile(card).Column(column =>
                {
                    SectionTitle(column, "Distribución de puntuaciones", "Preguntas por puntuación final (1–5)");
                    column.Item().Height(110).Svg(size => ScoreDistribution(report.FinalDistribution, size.Width, size.Height));
                }));
            }

            if (report.ShowAlignment)
            {
                row.RelativeItem().Element(card => Tile(card).Column(column =>
                {
                    SectionTitle(column, "Alineación de respuestas", "Autoevaluación frente a superior");
                    column.Item().Row(inner =>
                    {
                        inner.ConstantItem(100).Height(100)
                            .Svg(size => AlignmentDonut(report.AlignmentCounts, report.Snapshot.AlignmentPercentage, size.Width));
                        inner.ConstantItem(14);
                        inner.RelativeItem().AlignMiddle().Column(legend =>
                        {
                            legend.Spacing(5);
                            foreach (var level in Enum.GetValues<AlignmentLevel>())
                            {
                                var count = report.AlignmentCounts.GetValueOrDefault(level);
                                legend.Item().Row(line =>
                                {
                                    line.AutoItem().Element(c => LegendItem(c, AlignmentColor(level), AlignmentLabel(level)));
                                    line.RelativeItem().AlignRight().Text(count.ToString()).Bold().FontColor(Ink);
                                });
                            }
                        });
                    });
                }));
            }
        });
    }

    private static void ComposeHighlights(IContainer container, EvaluationResultReport report)
    {
        container.Row(row =>
        {
            row.Spacing(16);
            row.RelativeItem().Element(c => HighlightList(c, "Fortalezas", "Preguntas con mejor puntuación final",
                report.Strengths, "#0ca30c", "Ninguna pregunta alcanza 4 o más puntos."));
            row.RelativeItem().Element(c => HighlightList(c, "Áreas de mejora", "Preguntas con menor puntuación final",
                report.Improvements, "#d03b3b", "Ninguna pregunta queda en 3 puntos o menos."));
        });
    }

    private static void HighlightList(IContainer container, string title, string subtitle,
        IReadOnlyList<ScoredQuestion> items, string accent, string empty)
    {
        container.BorderLeft(3).BorderColor(accent).PaddingLeft(10).Column(column =>
        {
            SectionTitle(column, title, subtitle);
            if (items.Count == 0)
            {
                column.Item().Text(empty).FontSize(8.5f).Italic().FontColor(Muted);
                return;
            }
            column.Spacing(6);
            foreach (var item in items)
            {
                column.Item().Row(line =>
                {
                    line.RelativeItem().Column(text =>
                    {
                        text.Item().Text(item.Texto).SemiBold().FontColor(Ink);
                        text.Item().Text(item.Topic).FontSize(7.5f).FontColor(Muted);
                    });
                    line.ConstantItem(60).AlignRight().AlignMiddle().Column(score =>
                    {
                        score.Item().AlignRight().Height(6).Width(40).Svg(RatingDots(item.Score));
                        score.Item().AlignRight().Text($"{item.Score} / {EvaluationResultReport.MaxScore}").FontSize(7.5f).Bold().FontColor(Ink);
                    });
                });
            }
        });
    }

    /// <summary>Every question, grouped by topic: each respondent's answer, the final one and the alignment.</summary>
    private static void ComposeQuestions(IContainer container, EvaluationResultReport report)
    {
        var s = report.Snapshot;
        var groups = s.Questions
            .OrderBy(q => q.Orden)
            .GroupBy(q => string.IsNullOrWhiteSpace(q.Topic) ? "General" : q.Topic.Trim());

        container.Column(column =>
        {
            SectionTitle(column, "Detalle por pregunta", "Respuestas de cada evaluador y resultado final");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(5);
                    if (report.ShowSelf) columns.RelativeColumn(1.6f);
                    if (report.ShowManager) columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(2);
                    if (report.ShowAlignment) columns.RelativeColumn(1.8f);
                });

                table.Header(header =>
                {
                    var titles = new List<string> { "Pregunta" };
                    if (report.ShowSelf) titles.Add("Auto");
                    if (report.ShowManager) titles.Add("Superior");
                    titles.Add("Final");
                    if (report.ShowAlignment) titles.Add("Alineación");

                    foreach (var title in titles)
                    {
                        header.Cell().Background(BrandColor).PaddingVertical(5).PaddingHorizontal(6)
                            .Text(title).FontSize(8).SemiBold().FontColor(Colors.White);
                    }
                });

                var columnCount = 2 + (report.ShowSelf ? 1 : 0) + (report.ShowManager ? 1 : 0) + (report.ShowAlignment ? 1 : 0);

                foreach (var group in groups)
                {
                    table.Cell().ColumnSpan((uint)columnCount).Background(Surface).PaddingVertical(4).PaddingHorizontal(6)
                        .Text(group.Key).FontSize(8).Bold().FontColor(Ink);

                    foreach (var question in group)
                    {
                        QuestionCell(table).Column(cell =>
                        {
                            cell.Item().Text(question.Texto).SemiBold().FontColor(Ink);
                            if (question.Accepted && question.FinalSource is not null)
                            {
                                cell.Item().PaddingTop(2).Text($"Discrepancia aceptada · se toma la respuesta de {SourceLabel(question.FinalSource.Value)}")
                                    .FontSize(7).FontColor("#b45309");
                            }
                        });
                        if (report.ShowSelf) QuestionCell(table).Element(c => AnswerText(c, question.SelfAnswer));
                        if (report.ShowManager) QuestionCell(table).Element(c => AnswerText(c, question.ManagerAnswer));
                        QuestionCell(table).Element(c => FinalAnswer(c, question));
                        if (report.ShowAlignment) QuestionCell(table).Element(c => AlignmentBadge(c, question.Level));
                    }
                }
            });
        });
    }

    private static void AnswerText(IContainer container, string? answer) =>
        container.Text(answer ?? "—").FontColor(answer is null ? Muted : "#334155");

    private static void FinalAnswer(IContainer container, ResultQuestionSnapshot question)
    {
        var score = EvaluationResultReport.IsNumeric(question) ? EvaluationResultReport.Score(question.FinalAnswer) : null;
        if (score is null)
        {
            container.Text(question.FinalAnswer ?? "—").Bold().FontColor(question.FinalAnswer is null ? Muted : BrandColor);
            return;
        }

        container.Column(column =>
        {
            column.Item().Text($"{score} / {EvaluationResultReport.MaxScore}").Bold().FontColor(BrandColor);
            column.Item().PaddingTop(2).Height(6).Width(40).Svg(RatingDots(score.Value));
        });
    }

    private static void AlignmentBadge(IContainer container, AlignmentLevel level)
    {
        container.Row(row =>
        {
            row.AutoItem().AlignMiddle().Width(7).Height(7).Svg(
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"7\" height=\"7\"><circle cx=\"3.5\" cy=\"3.5\" r=\"3.5\" fill=\"{AlignmentColor(level)}\"/></svg>");
            row.AutoItem().AlignMiddle().PaddingLeft(4).Text(AlignmentLabel(level)).FontSize(8);
        });
    }

    private static IContainer QuestionCell(TableDescriptor table) =>
        table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(6).PaddingHorizontal(6);

    private static void ComposeNotes(IContainer container, EvaluationResultReport report)
    {
        var s = report.Snapshot;

        container.Background(Surface).CornerRadius(6).Padding(10).Column(column =>
        {
            column.Spacing(3);
            column.Item().Text("Cómo leer este informe").FontSize(8.5f).Bold().FontColor(Ink);
            column.Item().Text("• Las medias solo incluyen preguntas numéricas (estrellas y escala 1–5); las de selección se muestran pero no puntúan.").FontSize(7.5f);
            if (report.ShowManager)
                column.Item().Text("• La respuesta final es la del superior, salvo que RRHH haya aceptado una discrepancia a favor de la autoevaluación.").FontSize(7.5f);
            if (report.ShowAlignment)
                column.Item().Text("• Alineación: Alineado = misma respuesta; Leve = diferencia pequeña; Desequilibrio = diferencia relevante; No comparable = falta una de las dos respuestas.").FontSize(7.5f);
            if (s.AutoCompleted)
                column.Item().Text("• Alguna de las evaluaciones no se respondió dentro del plazo y se cerró automáticamente al completar el ciclo.").FontSize(7.5f).FontColor("#b45309");
            if (report.AcceptedCount > 0)
                column.Item().Text($"• RRHH resolvió {report.AcceptedCount} {(report.AcceptedCount == 1 ? "discrepancia" : "discrepancias")} durante la revisión.").FontSize(7.5f);
        });
    }

    private static void ComposeFooter(IContainer container, EvaluationResultSnapshot snapshot)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(0.75f).LineColor(Border);
            column.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text($"Documento confidencial · {snapshot.EvaluatedUserName} · {snapshot.CycleName}")
                    .FontSize(7.5f).FontColor(Muted);
                row.AutoItem().AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(7.5f).FontColor(Muted));
                    text.Span("Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        });
    }

    private static void SectionTitle(ColumnDescriptor column, string title, string subtitle)
    {
        column.Item().Text(title).FontSize(11).Bold().FontColor(Ink);
        column.Item().PaddingBottom(8).Text(subtitle).FontSize(8).FontColor(Muted);
    }

    private static void LegendItem(IContainer container, string color, string label)
    {
        container.Row(row =>
        {
            row.AutoItem().AlignMiddle().Width(8).Height(8).Svg(
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"8\" height=\"8\"><rect width=\"8\" height=\"8\" rx=\"2\" fill=\"{color}\"/></svg>");
            row.AutoItem().PaddingLeft(5).Text(label).FontSize(8);
        });
    }

    private static void DetailLine(ColumnDescriptor column, string label, string value)
    {
        column.Item().Text(text =>
        {
            text.Span($"{label}: ").SemiBold().FontColor(Muted);
            text.Span(value).FontColor(Ink);
        });
    }

    private static string ParticipationLabel(bool applies, bool completed) =>
        !applies ? "No aplica" : completed ? "Respondida" : "No respondida";

    private static string AlignmentLabel(AlignmentLevel level) => level switch
    {
        AlignmentLevel.Alineado => "Alineado",
        AlignmentLevel.Leve => "Leve",
        AlignmentLevel.Desequilibrio => "Desequilibrio",
        _ => "No comparable",
    };

    private static string SourceLabel(AcceptedAnswerSource source) => source switch
    {
        AcceptedAnswerSource.Superior => "el superior",
        _ => "la autoevaluación",
    };

    public static string EvaluationTypeLabel(EvaluationType type) => type switch
    {
        EvaluationType.Auto => "Autoevaluación",
        EvaluationType.Evaluacion180 => "Evaluación 180°",
        EvaluationType.Evaluacion360 => "Evaluación 360°",
        _ => type.ToString()
    };

    private static string FormatDate(DateTime date) => date.ToString("dd/MM/yyyy", Spanish);
}
