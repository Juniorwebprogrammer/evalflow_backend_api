using System.Text.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.AiAnalysis.AiAnalysisDto;
using evalflow_backend_api.Infrastructure.AI;
using evalflow_backend_api.Infrastructure.BackgroundJobs;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace evalflow_backend_api.Tests.Infrastructure.BackgroundJobs;

public sealed class AiAnalysisWorkerTests : IDisposable
{
    private const string ValidResult = """{"resumen":"Resumen","nivelRiesgo":"Bajo"}""";

    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly InMemoryDatabaseRoot _root = new();
    private readonly Mock<ILlmClient> _llm = new();
    private readonly Mock<IEncryptionService> _encryption = new();
    private readonly AiSettings _settings = new() { Enabled = true, ApiKey = "key", MaxRequestsPerDay = 100 };
    private readonly ServiceProvider _services;

    public AiAnalysisWorkerTests()
    {
        _encryption.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(p => $"enc:{p}");
        _encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(c => c[4..]);
        SetupLlm(new LlmCompletion(ValidResult, "test-model", 100, 50));

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName, _root));
        services.AddSingleton(_llm.Object);
        services.AddSingleton(_encryption.Object);
        _services = services.BuildServiceProvider();
    }

    public void Dispose() => _services.Dispose();

    private void SetupLlm(LlmCompletion completion) => _llm
        .Setup(l => l.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(completion);

    private void SetupLlmThrows(Exception ex) => _llm
        .Setup(l => l.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(ex);

    private AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName, _root).Options);

    private AiAnalysisWorker CreateSut() => new(_services.GetRequiredService<IServiceScopeFactory>(), new AiAnalysisQueueSignal(),
        Options.Create(_settings), NullLogger<AiAnalysisWorker>.Instance);

    private async Task<int> EnqueueAsync(int attempts = 0)
    {
        await using var db = CreateDbContext();
        var company = TestDataFactory.CreateCompany();
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var cycle = new EvaluationCycle
        {
            Nombre = "Q1", FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(1),
            EmpresaID = company.Id, TipoEvaluación = EvaluationType.Evaluacion360,
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        var analysis = new AiEvaluationAnalysis
        {
            EmpresaID = company.Id, EvaluationCycleId = cycle.Id, TemplateId = 1, EvaluatedUserId = 1, RequestedByUserId = 1,
            InputHash = "hash", InputEncrypted = "enc:{\"preguntas\":[]}", Attempts = attempts,
            NextAttemptAt = DateTime.UtcNow.AddSeconds(-1),
        };
        db.AiEvaluationAnalyses.Add(analysis);
        await db.SaveChangesAsync();
        return analysis.Id;
    }

    private AiEvaluationAnalysis Reload(int id)
    {
        using var db = CreateDbContext();
        return db.AiEvaluationAnalyses.Single(a => a.Id == id);
    }

    [Fact]
    public async Task ProcessNextAsync_Success_StoresEncryptedResultAndClearsInput()
    {
        var id = await EnqueueAsync();

        var processed = await CreateSut().ProcessNextAsync(CancellationToken.None);

        processed.Should().BeTrue();
        _llm.Verify(l => l.CompleteJsonAsync(It.IsAny<string>(), "{\"preguntas\":[]}", It.IsAny<string>(), It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
        var stored = Reload(id);
        stored.Estado.Should().Be(AiAnalysisStatus.Completado);
        stored.Model.Should().Be("test-model");
        stored.PromptTokens.Should().Be(100);
        stored.InputEncrypted.Should().BeEmpty();
        stored.FechaCompletado.Should().NotBeNull();
        JsonSerializer.Deserialize<AiAnalysisResultDto>(stored.ResultEncrypted![4..])!.Resumen.Should().Be("Resumen");
    }

    [Fact]
    public async Task ProcessNextAsync_NothingQueued_ReturnsFalse()
    {
        (await CreateSut().ProcessNextAsync(CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessNextAsync_RateLimited_PostponesWithoutSpendingAttempt()
    {
        SetupLlmThrows(new LlmRateLimitedException("429", TimeSpan.FromMinutes(2)));
        var id = await EnqueueAsync();

        await CreateSut().ProcessNextAsync(CancellationToken.None);

        var stored = Reload(id);
        stored.Estado.Should().Be(AiAnalysisStatus.Pendiente);
        stored.Attempts.Should().Be(0);
        stored.NextAttemptAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task ProcessNextAsync_TransientFailure_SchedulesRetry()
    {
        SetupLlmThrows(new LlmException("timeout", true));
        var id = await EnqueueAsync();

        await CreateSut().ProcessNextAsync(CancellationToken.None);

        var stored = Reload(id);
        stored.Estado.Should().Be(AiAnalysisStatus.Pendiente);
        stored.Attempts.Should().Be(1);
        stored.ErrorMensaje.Should().Be("timeout");
    }

    [Fact]
    public async Task ProcessNextAsync_InvalidJsonOnLastAttempt_MarksAsError()
    {
        SetupLlm(new LlmCompletion("no json", "m", null, null));
        var id = await EnqueueAsync(attempts: AiAnalysisWorker.MaxAttempts - 1);

        await CreateSut().ProcessNextAsync(CancellationToken.None);

        var stored = Reload(id);
        stored.Estado.Should().Be(AiAnalysisStatus.Error);
        stored.InputEncrypted.Should().BeEmpty();
        stored.ErrorMensaje.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessNextAsync_NonTransientFailure_MarksAsErrorImmediately()
    {
        SetupLlmThrows(new LlmException("401 invalid key", false));
        var id = await EnqueueAsync();

        await CreateSut().ProcessNextAsync(CancellationToken.None);

        Reload(id).Estado.Should().Be(AiAnalysisStatus.Error);
    }

    [Fact]
    public async Task ProcessNextAsync_DailyCapReached_StopsProcessing()
    {
        _settings.MaxRequestsPerDay = 1;
        await EnqueueAsync();
        var second = await EnqueueAsync();
        var sut = CreateSut();

        (await sut.ProcessNextAsync(CancellationToken.None)).Should().BeTrue();
        (await sut.ProcessNextAsync(CancellationToken.None)).Should().BeFalse();

        Reload(second).Estado.Should().Be(AiAnalysisStatus.Pendiente);
    }
}
