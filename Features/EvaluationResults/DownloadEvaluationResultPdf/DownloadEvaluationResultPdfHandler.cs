using System.Globalization;
using System.Text;
using System.Text.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.EvaluationResults.EvaluationResultsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.EvaluationResults.DownloadEvaluationResultPdf;

public class DownloadEvaluationResultPdfHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService,
    ILogger<DownloadEvaluationResultPdfHandler> logger
) : IRequestHandler<DownloadEvaluationResultPdfRecord, IResult>
{
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

        var pdf = EvaluationResultPdfDocument.Generate(snapshot);

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
