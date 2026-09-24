using System.Text;
using System.Text.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationResults.DownloadEvaluationResultPdf;
using evalflow_backend_api.Features.EvaluationResults.EvaluationResultsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QuestPDF.Infrastructure;

namespace evalflow_backend_api.Tests.Features.EvaluationResults.DownloadEvaluationResultPdf;

public class DownloadEvaluationResultPdfHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IEncryptionService> _encryptionService = new();

    public DownloadEvaluationResultPdfHandlerTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        _encryptionService.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(cipher => cipher[4..]);
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");
    }

    private DownloadEvaluationResultPdfHandler CreateSut(AppDbContext db) =>
        new(db, _currentUser.Object, _encryptionService.Object, NullLogger<DownloadEvaluationResultPdfHandler>.Instance);

    private void SignInAs(User user)
    {
        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());
        _currentUser.Setup(c => c.GetRol()).Returns(user.Rol);
    }

    private static async Task<EvaluationResult> AddResultAsync(AppDbContext db, ResultsScenario s, string snapshot)
    {
        var result = new EvaluationResult
        {
            EvaluationCycleId = s.Cycle.Id,
            TemplateId = s.Template.Id,
            EvaluatedUserId = s.Employee.Id,
            CompletedByUserId = s.Rrhh.Id,
            EncryptedSnapshot = $"enc:{snapshot}",
        };
        db.EvaluationResults.Add(result);
        await db.SaveChangesAsync();
        return result;
    }

    private static string Snapshot() => JsonSerializer.Serialize(new EvaluationResultSnapshot(
        "Acme Corp", "Q1 Ñandú", EvaluationType.Evaluacion360, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow,
        "Desempeño", null, "Enrique User", "employee@example.com", "Comercial", "Ventas", "Marta User",
        true, false, true, DateTime.UtcNow, 4.5, 3, 4.5, 66.67,
        [
            new ResultQuestionSnapshot(1, "Cumple plazos", QuestionType.Escala1a5, "General", 1, "5", "2", "5",
                AcceptedAnswerSource.Autoevaluacion, AlignmentLevel.Desequilibrio, true),
            new ResultQuestionSnapshot(2, "Fortalezas", QuestionType.Seleccion, "General", 2, "A, B", "A", "A",
                AcceptedAnswerSource.Superior, AlignmentLevel.Desequilibrio, false),
            new ResultQuestionSnapshot(3, "Sin respuesta", QuestionType.Estrellas, "General", 3, null, null, null,
                null, AlignmentLevel.NoComparable, false),
        ]));

    [Fact]
    public async Task Handle_WithoutUserClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var result = await CreateSut(db).Handle(new DownloadEvaluationResultPdfRecord(1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Handle_ForEvaluatedEmployee_ReturnsPdf()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ResultsTestData.SeedAsync(db, raw => $"enc:{raw}");
        var stored = await AddResultAsync(db, s, Snapshot());
        SignInAs(s.Employee);

        var result = await CreateSut(db).Handle(new DownloadEvaluationResultPdfRecord(stored.Id), CancellationToken.None);

        var file = result.Should().BeOfType<FileContentHttpResult>().Subject;
        file.ContentType.Should().Be("application/pdf");
        file.FileDownloadName.Should().Be("informe-evaluacion-q1-nandu-enrique-user.pdf");
        Encoding.ASCII.GetString(file.FileContents.Span[..4]).Should().Be("%PDF");
    }

    [Fact]
    public async Task Handle_ForRrhhOfSameCompany_ReturnsPdf()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ResultsTestData.SeedAsync(db, raw => $"enc:{raw}");
        var stored = await AddResultAsync(db, s, Snapshot());
        SignInAs(s.Rrhh);

        var result = await CreateSut(db).Handle(new DownloadEvaluationResultPdfRecord(stored.Id), CancellationToken.None);

        result.Should().BeOfType<FileContentHttpResult>();
    }

    [Fact]
    public async Task Handle_ForAnotherEmployee_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ResultsTestData.SeedAsync(db, raw => $"enc:{raw}");
        var stored = await AddResultAsync(db, s, Snapshot());
        SignInAs(s.Manager);

        var result = await CreateSut(db).Handle(new DownloadEvaluationResultPdfRecord(stored.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_ForRrhhOfAnotherCompany_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ResultsTestData.SeedAsync(db, raw => $"enc:{raw}");
        var stored = await AddResultAsync(db, s, Snapshot());
        SignInAs(s.Rrhh);
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-2");

        var result = await CreateSut(db).Handle(new DownloadEvaluationResultPdfRecord(stored.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithUnreadableSnapshot_ReturnsServerError()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ResultsTestData.SeedAsync(db, raw => $"enc:{raw}");
        var stored = await AddResultAsync(db, s, "not-json");
        SignInAs(s.Employee);

        var result = await CreateSut(db).Handle(new DownloadEvaluationResultPdfRecord(stored.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status500InternalServerError);
    }
}
