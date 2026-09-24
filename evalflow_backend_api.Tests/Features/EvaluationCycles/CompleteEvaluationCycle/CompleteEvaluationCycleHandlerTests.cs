using System.Text.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationComparisons.GetCycleComparisons;
using evalflow_backend_api.Features.EvaluationCycles.CompleteEvaluationCycle;
using evalflow_backend_api.Features.EvaluationResults.EvaluationResultsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Frontend;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using evalflow_backend_api.Tests.Common;
using evalflow_backend_api.Tests.Features.EvaluationResults;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationCycles.CompleteEvaluationCycle;

public class CompleteEvaluationCycleHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IEncryptionService> _encryptionService = new();
    private readonly FakeEmailService _emailService = new();

    public CompleteEvaluationCycleHandlerTests()
    {
        _encryptionService.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(plain => $"enc:{plain}");
        _encryptionService.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(cipher => cipher[4..]);
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");
    }

    private CompleteEvaluationCycleHandler CreateSut(AppDbContext db)
    {
        var comparisons = new GetCycleComparisonsHandler(db, _currentUser.Object, _encryptionService.Object,
            NullLogger<GetCycleComparisonsHandler>.Instance);
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GetCycleComparisonsRecord>(), It.IsAny<CancellationToken>()))
            .Returns<GetCycleComparisonsRecord, CancellationToken>((record, ct) => comparisons.Handle(record, ct));

        return new CompleteEvaluationCycleHandler(db, _currentUser.Object, _encryptionService.Object, sender.Object, _emailService,
            Options.Create(new FrontendSettings { BaseUrl = "http://frontend.test" }),
            NullLogger<CompleteEvaluationCycleHandler>.Instance);
    }

    private async Task<ResultsScenario> SeedAsync(AppDbContext db, EvaluationType tipo = EvaluationType.Evaluacion360, bool managerCompleted = true)
    {
        var s = await ResultsTestData.SeedAsync(db, raw => $"enc:{raw}", tipo: tipo, managerCompleted: managerCompleted);
        _currentUser.Setup(c => c.GetUserId()).Returns(s.Rrhh.Id.ToString());
        return s;
    }

    private static async Task AcceptAsync(AppDbContext db, ResultsScenario s, AcceptedAnswerSource source)
    {
        db.DiscrepancyAcceptances.Add(new DiscrepancyAcceptance
        {
            EvaluationCycleId = s.Cycle.Id,
            TemplateId = s.Template.Id,
            EvaluatedUserId = s.Employee.Id,
            QuestionId = s.Imbalanced.Id,
            AcceptedSource = source,
            AcceptedByUserId = s.Rrhh.Id,
        });
        await db.SaveChangesAsync();
    }

    private static EvaluationResultSnapshot ReadSnapshot(EvaluationResult result) =>
        JsonSerializer.Deserialize<EvaluationResultSnapshot>(result.EncryptedSnapshot[4..])!;

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var result = await CreateSut(db).Handle(new CompleteEvaluationCycleRecord(1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Handle_WithCycleFromAnotherTenant_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ResultsTestData.SeedAsync(db, raw => $"enc:{raw}", tenant: "tenant-2");
        _currentUser.Setup(c => c.GetUserId()).Returns(s.Rrhh.Id.ToString());

        var result = await CreateSut(db).Handle(new CompleteEvaluationCycleRecord(s.Cycle.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithPendingImbalances_ReturnsConflictAndChangesNothing()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);

        var result = await CreateSut(db).Handle(new CompleteEvaluationCycleRecord(s.Cycle.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status409Conflict);
        db.EvaluationResults.Should().BeEmpty();
        (await db.EvaluationCycles.AsNoTracking().SingleAsync()).FechaCompletado.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenAlreadyCompleted_ReturnsConflict()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);
        s.Cycle.FechaCompletado = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(new CompleteEvaluationCycleRecord(s.Cycle.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Handle_WithAcceptedImbalance_UsesChosenAnswerAndClosesCycle()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);
        await AcceptAsync(db, s, AcceptedAnswerSource.Autoevaluacion);

        var result = await CreateSut(db).Handle(new CompleteEvaluationCycleRecord(s.Cycle.Id), CancellationToken.None);

        var response = result.Should().BeOfType<Ok<CompleteEvaluationCycleResponse>>().Subject.Value!;
        response.ResultsGenerated.Should().Be(1);
        response.AutoCompletedSubmissions.Should().Be(0);

        var cycle = await db.EvaluationCycles.AsNoTracking().SingleAsync();
        cycle.FechaCompletado.Should().NotBeNull();
        cycle.Activo.Should().BeFalse();

        var stored = await db.EvaluationResults.SingleAsync();
        stored.EvaluatedUserId.Should().Be(s.Employee.Id);
        stored.CompletedByUserId.Should().Be(s.Rrhh.Id);

        var snapshot = ReadSnapshot(stored);
        snapshot.EvaluatedUserName.Should().Be("Enrique User");
        snapshot.ManagerName.Should().Be("Marta User");
        snapshot.Cargo.Should().Be("Comercial");
        snapshot.Departamento.Should().Be("Ventas");
        snapshot.AutoCompleted.Should().BeFalse();

        var imbalanced = snapshot.Questions.Single(q => q.QuestionId == s.Imbalanced.Id);
        imbalanced.SelfAnswer.Should().Be("5");
        imbalanced.ManagerAnswer.Should().Be("2");
        imbalanced.FinalAnswer.Should().Be("5");
        imbalanced.FinalSource.Should().Be(AcceptedAnswerSource.Autoevaluacion);
        imbalanced.Accepted.Should().BeTrue();
        imbalanced.Level.Should().Be(AlignmentLevel.Desequilibrio);

        var aligned = snapshot.Questions.Single(q => q.QuestionId == s.Aligned.Id);
        aligned.FinalAnswer.Should().Be("4");
        aligned.FinalSource.Should().Be(AcceptedAnswerSource.Superior);
        aligned.Accepted.Should().BeFalse();

        snapshot.Questions.Single(q => q.QuestionId == s.Selection.Id).FinalAnswer.Should().Be("A");
        snapshot.AverageFinal.Should().Be(4.5);
        stored.AverageFinal.Should().Be(4.5);

        _emailService.SentEmails.Should().ContainSingle(e => e.To == s.Employee.Email)
            .Which.HtmlBody.Should().Contain("http://frontend.test/dashboard/resultados-evaluacion");
    }

    [Fact]
    public async Task Handle_WithIncompleteSubmission_AutoCompletesItWithCurrentValues()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db, managerCompleted: false);

        var result = await CreateSut(db).Handle(new CompleteEvaluationCycleRecord(s.Cycle.Id), CancellationToken.None);

        result.Should().BeOfType<Ok<CompleteEvaluationCycleResponse>>().Subject.Value!.AutoCompletedSubmissions.Should().Be(1);
        (await db.EvaluationSubmissions.AsNoTracking().SingleAsync(x => x.Id == s.ManagerSubmission.Id)).IsCompleted.Should().BeTrue();

        var snapshot = ReadSnapshot(await db.EvaluationResults.SingleAsync());
        snapshot.AutoCompleted.Should().BeTrue();
        snapshot.ManagerCompleted.Should().BeFalse();
        snapshot.SelfCompleted.Should().BeTrue();

        var imbalanced = snapshot.Questions.Single(q => q.QuestionId == s.Imbalanced.Id);
        imbalanced.ManagerAnswer.Should().BeNull();
        imbalanced.FinalAnswer.Should().Be("5");
        imbalanced.FinalSource.Should().Be(AcceptedAnswerSource.Autoevaluacion);

        snapshot.Questions.Single(q => q.QuestionId == s.Aligned.Id).FinalSource.Should().Be(AcceptedAnswerSource.Superior);
    }

    [Fact]
    public async Task Handle_WithNon360Cycle_CompletesWithoutCheckingImbalances()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db, EvaluationType.Evaluacion180);

        var result = await CreateSut(db).Handle(new CompleteEvaluationCycleRecord(s.Cycle.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var snapshot = ReadSnapshot(await db.EvaluationResults.SingleAsync());
        snapshot.Questions.Should().OnlyContain(q => q.Level == AlignmentLevel.NoComparable);
    }

    [Fact]
    public async Task Handle_WithoutSubmissions_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db, EvaluationType.Evaluacion180);
        db.EvaluationSubmissions.RemoveRange(db.EvaluationSubmissions);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(new CompleteEvaluationCycleRecord(s.Cycle.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }
}
