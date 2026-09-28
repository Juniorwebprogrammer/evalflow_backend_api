using System.Globalization;
using System.Security;
using System.Text;
using evalflow_backend_api.Domain.Enums;

namespace evalflow_backend_api.Features.EvaluationResults.DownloadEvaluationResultPdf;

/// <summary>
/// SVG chart builders for the evaluation PDF (rendered by QuestPDF's
/// <c>Svg()</c>, sized in points). Series colors follow a fixed order validated
/// for color-vision deficiency; every chart is paired with visible values or a
/// legend, so nothing depends on color alone.
/// </summary>
public static class EvaluationResultPdfCharts
{
    public const string SeriesSelf = "#2a78d6";
    public const string SeriesManager = "#eb6834";
    public const string Track = "#e6effb";
    public const string Grid = "#e2e8f0";
    public const string Ink = "#0f172a";
    public const string Muted = "#64748b";

    private const string Font = "Lato, Arial, sans-serif";
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Status colors per alignment level — always shown with their label.</summary>
    public static string AlignmentColor(AlignmentLevel level) => level switch
    {
        AlignmentLevel.Alineado => "#0ca30c",
        AlignmentLevel.Leve => "#fab219",
        AlignmentLevel.Desequilibrio => "#d03b3b",
        _ => "#cbd5e1",
    };

    public static string FormatScore(double value) => value.ToString("0.0", Spanish);

    /// <summary>Row height used by <see cref="TopicBars"/>, so the caller can reserve the height.</summary>
    public static float TopicBarsHeight(int topicCount, bool twoSeries) =>
        topicCount * (twoSeries ? 34 : 24) + 18;

    /// <summary>
    /// Horizontal bars of each topic's average (0–5 axis): one bar per
    /// respondent (self / manager), value at the tip, gridline at every point.
    /// </summary>
    public static string TopicBars(IReadOnlyList<TopicScore> topics, bool showSelf, bool showManager, float width)
    {
        var twoSeries = showSelf && showManager;
        var rowHeight = twoSeries ? 34f : 24f;
        var height = TopicBarsHeight(topics.Count, twoSeries);
        const float labelWidth = 150f;
        const float valueWidth = 26f;
        var plotX = labelWidth;
        var plotWidth = Math.Max(width - labelWidth - valueWidth, 60);
        const float barHeight = 9f;

        var svg = Begin(width, height);

        for (var tick = 0; tick <= EvaluationResultReport.MaxScore; tick++)
        {
            var x = plotX + plotWidth * tick / EvaluationResultReport.MaxScore;
            svg.Append($"<rect x=\"{F(x - 0.375)}\" y=\"0\" width=\"0.75\" height=\"{F(height - 16)}\" fill=\"{Grid}\"/>");
            svg.Append(Text(x, height - 4, tick.ToString(), 7, Muted, "middle"));
        }

        for (var i = 0; i < topics.Count; i++)
        {
            var topic = topics[i];
            var top = i * rowHeight + 4;
            var labelY = top + (twoSeries ? barHeight + 3 : barHeight - 1);
            svg.Append(Text(0, labelY, Truncate(topic.Topic, 30), 8, Ink, "start", bold: true));

            var bars = new List<(double? Value, string Color)>();
            if (showSelf) bars.Add((topic.Self, SeriesSelf));
            if (showManager) bars.Add((topic.Manager, SeriesManager));

            for (var b = 0; b < bars.Count; b++)
            {
                var (value, color) = bars[b];
                var y = top + b * (barHeight + 2);
                if (value is null)
                {
                    svg.Append(Text(plotX + 2, y + barHeight - 1.5f, "sin respuesta", 7, Muted, "start"));
                    continue;
                }
                var barWidth = (float)(plotWidth * value.Value / EvaluationResultReport.MaxScore);
                svg.Append(RoundedEndBar(plotX, y, Math.Max(barWidth, 2), barHeight, color));
                svg.Append(Text(plotX + barWidth + 4, y + barHeight - 1.5f, FormatScore(value.Value), 7.5f, Ink, "start", bold: true));
            }
        }

        return End(svg);
    }

    /// <summary>Donut of questions per alignment level, with the alignment % in the center.</summary>
    public static string AlignmentDonut(IReadOnlyDictionary<AlignmentLevel, int> counts, double? alignmentPercentage, float size)
    {
        var total = counts.Values.Sum();
        var thickness = size * 0.16f;
        var radius = (size - thickness) / 2;
        var circumference = 2 * Math.PI * radius;
        var center = size / 2;
        var svg = Begin(size, size);

        svg.Append($"<circle cx=\"{F(center)}\" cy=\"{F(center)}\" r=\"{F(radius)}\" fill=\"none\" stroke=\"#f1f5f9\" stroke-width=\"{F(thickness)}\"/>");

        var visible = Enum.GetValues<AlignmentLevel>().Where(l => counts.GetValueOrDefault(l) > 0).ToList();
        var gap = visible.Count > 1 ? 1.5 : 0;
        var offset = 0.0;
        foreach (var level in visible)
        {
            var length = counts[level] / (double)total * circumference;
            var dash = Math.Max(length - gap, 0.5);
            // Rotate -90° so the first segment starts at 12 o'clock.
            svg.Append($"<circle cx=\"{F(center)}\" cy=\"{F(center)}\" r=\"{F(radius)}\" fill=\"none\" stroke=\"{AlignmentColor(level)}\" " +
                       $"stroke-width=\"{F(thickness)}\" stroke-dasharray=\"{F(dash)} {F(circumference - dash)}\" " +
                       $"stroke-dashoffset=\"{F(-offset)}\" transform=\"rotate(-90 {F(center)} {F(center)})\"/>");
            offset += length;
        }

        var headline = alignmentPercentage is null ? "—" : $"{Math.Round(alignmentPercentage.Value):0}%";
        svg.Append(Text(center, center + 3, headline, size * 0.17f, Ink, "middle", bold: true));
        svg.Append(Text(center, center + size * 0.14f, "alineación", size * 0.07f, Muted, "middle"));

        return End(svg);
    }

    /// <summary>Column chart: how many questions got each final score (1–5), count on each cap.</summary>
    public static string ScoreDistribution(IReadOnlyList<int> distribution, float width, float height)
    {
        var max = Math.Max(distribution.DefaultIfEmpty(0).Max(), 1);
        var plotTop = 12f;
        var plotBottom = height - 14;
        var plotHeight = plotBottom - plotTop;
        var slot = width / distribution.Count;
        var columnWidth = Math.Min(slot * 0.55f, 24);
        var svg = Begin(width, height);

        svg.Append($"<line x1=\"0\" y1=\"{F(plotBottom)}\" x2=\"{F(width)}\" y2=\"{F(plotBottom)}\" stroke=\"#cbd5e1\" stroke-width=\"0.75\"/>");

        for (var i = 0; i < distribution.Count; i++)
        {
            var cx = slot * i + slot / 2;
            var count = distribution[i];
            var columnHeight = (float)(plotHeight * count / (double)max);
            if (count > 0)
            {
                svg.Append(RoundedTopColumn(cx - columnWidth / 2, plotBottom - columnHeight, columnWidth, columnHeight, SeriesSelf));
            }
            svg.Append(Text(cx, plotBottom - columnHeight - 3, count.ToString(), 7.5f, Ink, "middle", bold: true));
            svg.Append(Text(cx, height - 3, $"{i + 1}", 7.5f, Muted, "middle"));
        }

        return End(svg);
    }

    /// <summary>Horizontal meter (value / max) on a light track of the same hue.</summary>
    public static string Meter(double value, double max, float width, float height = 5, string color = SeriesSelf)
    {
        var ratio = max > 0 ? Math.Clamp(value / max, 0, 1) : 0;
        var svg = Begin(width, height);
        svg.Append($"<rect x=\"0\" y=\"0\" width=\"{F(width)}\" height=\"{F(height)}\" rx=\"{F(height / 2)}\" fill=\"{Track}\"/>");
        if (ratio > 0)
        {
            svg.Append($"<rect x=\"0\" y=\"0\" width=\"{F((float)(width * ratio))}\" height=\"{F(height)}\" rx=\"{F(height / 2)}\" fill=\"{color}\"/>");
        }
        return End(svg);
    }

    /// <summary>Five dots, the first <paramref name="score"/> filled — the final score of a numeric question.</summary>
    public static string RatingDots(int score, float dotSize = 6, float gap = 2.5f)
    {
        var width = EvaluationResultReport.MaxScore * dotSize + (EvaluationResultReport.MaxScore - 1) * gap;
        var svg = Begin(width, dotSize);
        for (var i = 0; i < EvaluationResultReport.MaxScore; i++)
        {
            var cx = i * (dotSize + gap) + dotSize / 2;
            var fill = i < score ? SeriesSelf : Track;
            svg.Append($"<circle cx=\"{F(cx)}\" cy=\"{F(dotSize / 2)}\" r=\"{F(dotSize / 2)}\" fill=\"{fill}\"/>");
        }
        return End(svg);
    }

    /// <summary>Bar with a 3pt rounded data-end and a square baseline end.</summary>
    private static string RoundedEndBar(float x, float y, float width, float height, string color)
    {
        var r = Math.Min(3f, width / 2);
        return $"<path d=\"M{F(x)},{F(y)} H{F(x + width - r)} Q{F(x + width)},{F(y)} {F(x + width)},{F(y + r)} " +
               $"V{F(y + height - r)} Q{F(x + width)},{F(y + height)} {F(x + width - r)},{F(y + height)} H{F(x)} Z\" fill=\"{color}\"/>";
    }

    /// <summary>Column with a 3pt rounded top and a square baseline end.</summary>
    private static string RoundedTopColumn(float x, float y, float width, float height, string color)
    {
        var r = Math.Min(3f, Math.Min(width / 2, height));
        return $"<path d=\"M{F(x)},{F(y + height)} V{F(y + r)} Q{F(x)},{F(y)} {F(x + r)},{F(y)} H{F(x + width - r)} " +
               $"Q{F(x + width)},{F(y)} {F(x + width)},{F(y + r)} V{F(y + height)} Z\" fill=\"{color}\"/>";
    }

    private static StringBuilder Begin(float width, float height) =>
        new($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(width)}\" height=\"{F(height)}\" viewBox=\"0 0 {F(width)} {F(height)}\">");

    private static string End(StringBuilder svg) => svg.Append("</svg>").ToString();

    private static string Text(float x, float y, string value, float size, string color, string anchor, bool bold = false) =>
        $"<text x=\"{F(x)}\" y=\"{F(y)}\" font-family=\"{Font}\" font-size=\"{F(size)}\" fill=\"{color}\" " +
        $"text-anchor=\"{anchor}\"{(bold ? " font-weight=\"bold\"" : "")}>{SecurityElement.Escape(value)}</text>";

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
