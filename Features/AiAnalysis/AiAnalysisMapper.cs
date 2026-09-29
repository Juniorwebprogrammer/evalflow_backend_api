using System.Text.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.AiAnalysis.AiAnalysisDto;
using evalflow_backend_api.Infrastructure.Security.Encryption;

namespace evalflow_backend_api.Features.AiAnalysis;

public static class AiAnalysisMapper
{
    public static AiEvaluationAnalysisDto ToDto(AiEvaluationAnalysis analysis, IEncryptionService encryptionService, ILogger logger)
    {
        return new AiEvaluationAnalysisDto(
            analysis.Id,
            analysis.EvaluationCycleId,
            analysis.TemplateId,
            analysis.EvaluatedUserId,
            analysis.Estado,
            analysis.Model,
            analysis.FechaCreacion,
            analysis.FechaCompletado,
            analysis.ErrorMensaje,
            DecryptResult(analysis, encryptionService, logger));
    }

    private static AiAnalysisResultDto? DecryptResult(AiEvaluationAnalysis analysis, IEncryptionService encryptionService, ILogger logger)
    {
        if (string.IsNullOrEmpty(analysis.ResultEncrypted)) return null;

        try
        {
            return JsonSerializer.Deserialize<AiAnalysisResultDto>(encryptionService.Decrypt(analysis.ResultEncrypted));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AiAnalysis: no se pudo leer el resultado del análisis {AnalysisId}.", analysis.Id);
            return null;
        }
    }
}
