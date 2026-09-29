using System.Text.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.AiAnalysis;
using evalflow_backend_api.Features.AiAnalysis.AiAnalysisDto;
using evalflow_backend_api.Infrastructure.AI;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using evalflow_backend_api.Infrastructure.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Infrastructure.BackgroundJobs;

/// <summary>
/// Sends queued <see cref="AiEvaluationAnalysis"/> rows to the LLM one at a time. Free tiers
/// have small per-minute and per-day quotas, so calls are spaced out, capped per day across
/// every company, and rate-limit answers (429) just postpone the analysis.
/// </summary>
public class AiAnalysisWorker(
    IServiceScopeFactory scopeFactory,
    AiAnalysisQueueSignal signal,
    IOptions<AiSettings> options,
    ILogger<AiAnalysisWorker> logger) : BackgroundService
{
    public const int MaxAttempts = 3;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MinRetryAfter = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("AiAnalysisWorker: IA desactivada (Ai:Enabled = false), el worker no se inicia.");
            return;
        }

        await ResetInterruptedAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (await ProcessNextAsync(stoppingToken))
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, options.Value.MinSecondsBetweenRequests)), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AiAnalysisWorker: error al procesar la cola de análisis.");
            }

            await signal.WaitAsync(PollInterval, stoppingToken);
        }
    }

    /// <summary>Analyses left "Procesando" by a restart go back to the queue.</summary>
    private async Task ResetInterruptedAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var interrupted = await dbContext.AiEvaluationAnalyses
            .Where(a => a.Estado == AiAnalysisStatus.Procesando)
            .ToListAsync(cancellationToken);
        foreach (var analysis in interrupted) analysis.Estado = AiAnalysisStatus.Pendiente;

        if (interrupted.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Processes the next due analysis. False when there's nothing to do (or the daily cap is reached).</summary>
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var llm = scope.ServiceProvider.GetRequiredService<ILlmClient>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var hubContext = scope.ServiceProvider.GetService<IHubContext<DashboardHub>>();

        if (DailyCapReached())
        {
            logger.LogWarning("AiAnalysisWorker: alcanzado el tope diario de {Max} peticiones; se reanuda mañana.",
                options.Value.MaxRequestsPerDay);
            return false;
        }

        var now = DateTime.UtcNow;
        var analysis = await dbContext.AiEvaluationAnalyses
            .Include(a => a.Cycle)
            .Include(a => a.Empresa)
            .Where(a => a.Estado == AiAnalysisStatus.Pendiente && a.NextAttemptAt <= now)
            .OrderBy(a => a.FechaCreacion)
            .ThenBy(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (analysis is null) return false;

        analysis.Estado = AiAnalysisStatus.Procesando;
        await dbContext.SaveChangesAsync(cancellationToken);

        await AnalyseAsync(analysis, llm, encryptionService, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (analysis.Estado is AiAnalysisStatus.Completado or AiAnalysisStatus.Error)
            await NotifyAsync(hubContext, analysis, cancellationToken);

        return true;
    }

    /// <summary>Calls made today (UTC). A restart resets it; past that, the provider's own 429s postpone the queue.</summary>
    private DateTime _counterDay = DateTime.UtcNow.Date;
    private int _requestsToday;

    private bool DailyCapReached()
    {
        var today = DateTime.UtcNow.Date;
        if (today != _counterDay)
        {
            _counterDay = today;
            _requestsToday = 0;
        }

        var max = options.Value.MaxRequestsPerDay;
        return max > 0 && _requestsToday >= max;
    }

    private async Task AnalyseAsync(AiEvaluationAnalysis analysis, ILlmClient llm, IEncryptionService encryptionService,
        CancellationToken cancellationToken)
    {
        try
        {
            var input = encryptionService.Decrypt(analysis.InputEncrypted);
            var tipo = analysis.Cycle?.TipoEvaluación ?? EvaluationType.Evaluacion360;

            analysis.Attempts++;
            _requestsToday++;
            var completion = await llm.CompleteJsonAsync(AiAnalysisPrompt.SystemPrompt(tipo), input,
                AiAnalysisPrompt.SchemaName, AiAnalysisPrompt.Schema, cancellationToken);
            var result = AiAnalysisResultParser.Parse(completion.Content);

            analysis.ResultEncrypted = encryptionService.Encrypt(JsonSerializer.Serialize(result));
            analysis.Model = completion.Model;
            analysis.PromptTokens = completion.PromptTokens;
            analysis.CompletionTokens = completion.CompletionTokens;
            analysis.Estado = AiAnalysisStatus.Completado;
            analysis.FechaCompletado = DateTime.UtcNow;
            analysis.ErrorMensaje = null;
            analysis.InputEncrypted = string.Empty;

            logger.LogInformation("AiAnalysisWorker: análisis {AnalysisId} completado ({Model}, {PromptTokens}+{CompletionTokens} tokens).",
                analysis.Id, completion.Model, completion.PromptTokens, completion.CompletionTokens);
        }
        catch (LlmRateLimitedException ex)
        {
            // Hitting the provider's quota isn't the analysis' fault: postpone without spending an attempt.
            analysis.Attempts = Math.Max(0, analysis.Attempts - 1);
            analysis.Estado = AiAnalysisStatus.Pendiente;
            analysis.NextAttemptAt = DateTime.UtcNow.Add(ex.RetryAfter < MinRetryAfter ? MinRetryAfter : ex.RetryAfter);
            logger.LogWarning("AiAnalysisWorker: límite del proveedor alcanzado, el análisis {AnalysisId} se reintentará a las {NextAttemptAt}.",
                analysis.Id, analysis.NextAttemptAt);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            RegisterFailure(analysis, ex);
        }
    }

    private void RegisterFailure(AiEvaluationAnalysis analysis, Exception ex)
    {
        var transient = ex is LlmException { IsTransient: true };
        analysis.ErrorMensaje = Truncate(ex.Message, 2000);

        if (!transient || analysis.Attempts >= MaxAttempts)
        {
            analysis.Estado = AiAnalysisStatus.Error;
            analysis.FechaCompletado = DateTime.UtcNow;
            analysis.InputEncrypted = string.Empty;
            analysis.ErrorMensaje = "No se pudo generar el análisis con IA. Inténtalo de nuevo más tarde.";
            logger.LogError(ex, "AiAnalysisWorker: el análisis {AnalysisId} se descarta tras {Attempts} intentos.",
                analysis.Id, analysis.Attempts);
            return;
        }

        analysis.Estado = AiAnalysisStatus.Pendiente;
        analysis.NextAttemptAt = DateTime.UtcNow.Add(RetryDelays[Math.Min(analysis.Attempts - 1, RetryDelays.Length - 1)]);
        logger.LogWarning(ex, "AiAnalysisWorker: fallo en el análisis {AnalysisId} (intento {Attempts}), se reintentará a las {NextAttemptAt}.",
            analysis.Id, analysis.Attempts, analysis.NextAttemptAt);
    }

    private async Task NotifyAsync(IHubContext<DashboardHub>? hubContext, AiEvaluationAnalysis analysis, CancellationToken cancellationToken)
    {
        var tenantId = analysis.Empresa?.IdentificationId;
        if (hubContext is null || string.IsNullOrWhiteSpace(tenantId)) return;

        try
        {
            await hubContext.Clients.Group(tenantId).SendAsync("AiAnalysisCompleted",
                new AiAnalysisCompletedNotification(analysis.Id, analysis.EvaluationCycleId, analysis.TemplateId,
                    analysis.EvaluatedUserId, analysis.Estado),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AiAnalysisWorker: no se pudo notificar el análisis {AnalysisId} por SignalR.", analysis.Id);
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
