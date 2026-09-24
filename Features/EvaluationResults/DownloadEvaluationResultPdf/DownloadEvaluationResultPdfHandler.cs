using System.Globalization;
using System.Text;
using System.Text.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationResults.EvaluationResultsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace evalflow_backend_api.Features.EvaluationResults.DownloadEvaluationResultPdf;

public class DownloadEvaluationResultPdfHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService,
    ILogger<DownloadEvaluationResultPdfHandler> logger
) : IRequestHandler<DownloadEvaluationResultPdfRecord, IResult>
{
    private const string BrandColor = "#2563eb";
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    public async Task<IResult> Handle(DownloadEvaluationResultPdfRecord request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(currentUser.GetUserId(), out var userId)) return Results.Unauthorized();

        var result = await dbContext.EvaluationResults
            .AsNoTracking()
            .Include(r => r.Cycle).ThenInclude(c => c!.Empresa)
            .FirstOrDefaultAsync(r => r.Id == request.ResultId, cancellationToken);

        if (result is null || !CanAccess(result, userId))
            return Results.NotFound(new { Message = "Resultado de evaluación no encontrado." });

        var snapshot = ReadSnapshot(result);
        if (snapshot is null)
            return Results.Problem("No se pudo leer el resultado de la evaluación.", statusCode: StatusCodes.Status500InternalServerError);

        var pdf = GeneratePdf(snapshot);

        return Results.File(pdf, "application/pdf", BuildFileName(snapshot));
    }

    private bool CanAccess(EvaluationResult result, int userId)
    {
        if (result.EvaluatedUserId == userId) return true;

        var role = currentUser.GetRol();
        var isPrivileged = role == AppRoles.Owner || role == AppRoles.Rrhh;

        return isPrivileged && result.Cycle!.Empresa!.IdentificationId == currentUser.GetIdentificationId();
    }

    private EvaluationResultSnapshot? ReadSnapshot(EvaluationResult result)
    {
        try
        {
            return JsonSerializer.Deserialize<EvaluationResultSnapshot>(encryptionService.Decrypt(result.EncryptedSnapshot));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DownloadEvaluationResultPdf: no se pudo leer el snapshot del resultado {ResultId}.", result.Id);
            return null;
        }
    }

    private static byte[] GeneratePdf(EvaluationResultSnapshot snapshot)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style.FontSize(9.5f).FontColor(Colors.Grey.Darken3));

                page.Header().Element(header => ComposeHeader(header, snapshot));
                page.Content().PaddingVertical(16).Element(content => ComposeContent(content, snapshot));
                page.Footer().Element(footer => ComposeFooter(footer, snapshot));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, EvaluationResultSnapshot snapshot)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(title =>
                {
                    title.Item().Text(text =>
                    {
                        text.Span("Eval").FontSize(20).Bold().FontColor(Colors.Grey.Darken4);
                        text.Span("Flow").FontSize(20).Bold().FontColor(BrandColor);
                    });
                    title.Item().Text("Informe de resultados de evaluación").FontSize(12).SemiBold();
                });
                row.ConstantItem(180).AlignRight().Column(meta =>
                {
                    meta.Item().AlignRight().Text(snapshot.CompanyName).SemiBold();
                    meta.Item().AlignRight().Text($"Completado el {FormatDate(snapshot.CompletedAt)}").FontSize(8.5f).FontColor(Colors.Grey.Medium);
                });
            });
            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(BrandColor);
        });
    }

    private static void ComposeContent(IContainer container, EvaluationResultSnapshot snapshot)
    {
        container.Column(column =>
        {
            column.Spacing(14);
            column.Item().Element(c => ComposeDetails(c, snapshot));
            column.Item().Element(c => ComposeSummary(c, snapshot));

            if (snapshot.AutoCompleted)
            {
                column.Item().Background(Colors.Amber.Lighten5).Padding(8)
                    .Text("Alguno de los formularios de esta evaluación no se envió a tiempo y se completó automáticamente con las respuestas que tenía guardadas.")
                    .FontSize(8.5f).FontColor(Colors.Amber.Darken4);
            }

            column.Item().Element(c => ComposeQuestions(c, snapshot));
        });
    }

    private static void ComposeDetails(IContainer container, EvaluationResultSnapshot snapshot)
    {
        container.Background(Colors.Grey.Lighten5).Padding(12).Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Spacing(3);
                DetailLine(left, "Empleado", snapshot.EvaluatedUserName);
                DetailLine(left, "Puesto", snapshot.Cargo ?? "—");
                DetailLine(left, "Departamento", snapshot.Departamento ?? "—");
                DetailLine(left, "Evaluador", snapshot.ManagerName ?? "—");
            });
            row.RelativeItem().Column(right =>
            {
                right.Spacing(3);
                DetailLine(right, "Ciclo", snapshot.CycleName);
                DetailLine(right, "Tipo", EvaluationTypeLabel(snapshot.TipoEvaluacion));
                DetailLine(right, "Periodo", $"{FormatDate(snapshot.FechaInicio)} – {FormatDate(snapshot.FechaFin)}");
                DetailLine(right, "Plantilla", snapshot.TemplateTitle);
            });
        });
    }

    private static void DetailLine(ColumnDescriptor column, string label, string value)
    {
        column.Item().Text(text =>
        {
            text.Span($"{label}: ").SemiBold().FontColor(Colors.Grey.Darken1);
            text.Span(value);
        });
    }

    private static void ComposeSummary(IContainer container, EvaluationResultSnapshot snapshot)
    {
        container.Row(row =>
        {
            row.Spacing(10);
            SummaryTile(row.RelativeItem(), "Puntuación final", FormatScore(snapshot.AverageFinal), BrandColor);
            SummaryTile(row.RelativeItem(), "Autoevaluación", FormatScore(snapshot.AverageSelf), Colors.Grey.Darken3);
            SummaryTile(row.RelativeItem(), "Superior", FormatScore(snapshot.AverageManager), Colors.Grey.Darken3);
            SummaryTile(row.RelativeItem(), "Equilibrio",
                snapshot.AlignmentPercentage is null ? "—" : $"{FormatScore(snapshot.AlignmentPercentage)} %", Colors.Green.Darken2);
        });
    }

    private static void SummaryTile(IContainer container, string label, string value, string color)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Item().Text(label.ToUpperInvariant()).FontSize(7.5f).SemiBold().FontColor(Colors.Grey.Medium);
            column.Item().PaddingTop(2).Text(value).FontSize(15).Bold().FontColor(color);
        });
    }

    private static void ComposeQuestions(IContainer container, EvaluationResultSnapshot snapshot)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(6).Text("Respuestas por pregunta").FontSize(11).SemiBold();

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2.4f);
                    columns.RelativeColumn(1.6f);
                });

                table.Header(header =>
                {
                    foreach (var title in new[] { "Pregunta", "Autoevaluación", "Superior", "Respuesta final", "Nivel" })
                    {
                        header.Cell().Background(BrandColor).PaddingVertical(5).PaddingHorizontal(6)
                            .Text(title).FontSize(8.5f).SemiBold().FontColor(Colors.White);
                    }
                });

                foreach (var question in snapshot.Questions.OrderBy(q => q.Orden))
                {
                    QuestionCell(table).Column(cell =>
                    {
                        cell.Item().Text(question.Texto).SemiBold();
                        cell.Item().Text(question.Topic).FontSize(7.5f).FontColor(Colors.Grey.Medium);
                    });
                    QuestionCell(table).Text(question.SelfAnswer ?? "—");
                    QuestionCell(table).Text(question.ManagerAnswer ?? "—");
                    QuestionCell(table).Column(cell =>
                    {
                        cell.Item().Text(question.FinalAnswer ?? "—").Bold().FontColor(BrandColor);
                        cell.Item().Text(FinalSourceLabel(question)).FontSize(7.5f).FontColor(Colors.Grey.Medium);
                    });
                    QuestionCell(table).Text(LevelLabel(question.Level)).FontSize(8.5f).FontColor(LevelColor(question.Level));
                }
            });
        });
    }

    private static IContainer QuestionCell(TableDescriptor table) =>
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(6).PaddingHorizontal(6);

    private static void ComposeFooter(IContainer container, EvaluationResultSnapshot snapshot)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text($"{snapshot.EvaluatedUserName} · {snapshot.CycleName}").FontSize(8).FontColor(Colors.Grey.Medium);
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Medium));
                text.Span("Página ");
                text.CurrentPageNumber();
                text.Span(" de ");
                text.TotalPages();
            });
        });
    }

    private static string FinalSourceLabel(ResultQuestionSnapshot question)
    {
        if (question.FinalSource is null) return "Sin respuesta";

        var source = question.FinalSource == AcceptedAnswerSource.Superior ? "del superior" : "de la autoevaluación";
        return question.Accepted ? $"Aceptada {source}" : $"Respuesta {source}";
    }

    private static string LevelLabel(AlignmentLevel level) => level switch
    {
        AlignmentLevel.Alineado => "Alineado",
        AlignmentLevel.Leve => "Diferencia leve",
        AlignmentLevel.Desequilibrio => "Desequilibrio",
        _ => "—"
    };

    private static string LevelColor(AlignmentLevel level) => level switch
    {
        AlignmentLevel.Alineado => Colors.Green.Darken2,
        AlignmentLevel.Leve => Colors.Amber.Darken3,
        AlignmentLevel.Desequilibrio => Colors.Red.Darken2,
        _ => Colors.Grey.Medium
    };

    private static string EvaluationTypeLabel(EvaluationType type) => type switch
    {
        EvaluationType.Auto => "Autoevaluación",
        EvaluationType.Evaluacion180 => "Evaluación 180°",
        EvaluationType.Evaluacion360 => "Evaluación 360°",
        _ => type.ToString()
    };

    private static string FormatDate(DateTime date) => date.ToString("dd/MM/yyyy", Spanish);

    private static string FormatScore(double? value) => value?.ToString("0.##", Spanish) ?? "—";

    private static string BuildFileName(EvaluationResultSnapshot snapshot)
    {
        var slug = Slugify($"{snapshot.CycleName}-{snapshot.EvaluatedUserName}");
        return $"informe-evaluacion-{slug}.pdf";
    }

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        }

        return string.Join('-', builder.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
