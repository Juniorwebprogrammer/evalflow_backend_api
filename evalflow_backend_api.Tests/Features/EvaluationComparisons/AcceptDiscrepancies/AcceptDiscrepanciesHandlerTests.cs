using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationComparisons.AcceptDiscrepancies;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using evalflow_backend_api.Tests.Features.EvaluationResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationComparisons.AcceptDiscrepancies;

public class AcceptDiscrepanciesHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public AcceptDiscrepanciesHandlerTests()
    {
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");
    }

    private AcceptDiscrepanciesHandler CreateSut(AppDbContext db) => new(db, _currentUser.Object);

    private async Task<ResultsScenario> SeedAsync(AppDbContext db, EvaluationType tipo = EvaluationType.Evaluacion360, bool managerCompleted = true)
    {
        var s = await ResultsTestData.SeedAsync(db, raw => $"enc:{raw}", tipo: tipo, managerCompleted: managerCompleted);
        _currentUser.Setup(c => c.GetUserId()).Returns(s.Rrhh.Id.ToString());
        return s;
    }

    private static AcceptDiscrepanciesRecord Request(ResultsScenario s, AcceptedAnswerSource source = AcceptedAnswerSource.Autoevaluacion, params int[] questionIds) =>
        new(s.Cycle.Id, s.Employee.Id, s.Template.Id, questionIds.Length == 0 ? [s.Imbalanced.Id] : questionIds.ToList(), source);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var result = await CreateSut(db).Handle(new AcceptDiscrepanciesRecord(1, 1, 1, [1], AcceptedAnswerSource.Superior), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Handle_WithoutQuestions_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);

        var result = await CreateSut(db).Handle(new AcceptDiscrepanciesRecord(s.Cycle.Id, s.Employee.Id, s.Template.Id, [], AcceptedAnswerSource.Superior), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithNon360Cycle_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db, EvaluationType.Evaluacion180);

        var result = await CreateSut(db).Handle(Request(s), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithCompletedCycle_ReturnsConflict()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);
        s.Cycle.FechaCompletado = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(Request(s), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Handle_WhenManagerHasNotAnswered_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db, managerCompleted: false);

        var result = await CreateSut(db).Handle(Request(s), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithQuestionFromAnotherTemplate_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);

        var result = await CreateSut(db).Handle(Request(s, AcceptedAnswerSource.Superior, s.Imbalanced.Id, 9999), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
        db.DiscrepancyAcceptances.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithValidRequest_StoresAcceptances()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);

        var result = await CreateSut(db).Handle(Request(s, AcceptedAnswerSource.Autoevaluacion, s.Imbalanced.Id, s.Aligned.Id), CancellationToken.None);

        result.Should().BeOfType<Ok<List<AcceptedDiscrepancyDto>>>().Subject.Value.Should().HaveCount(2);
        var stored = await db.DiscrepancyAcceptances.ToListAsync();
        stored.Should().HaveCount(2).And.OnlyContain(a =>
            a.AcceptedSource == AcceptedAnswerSource.Autoevaluacion && a.AcceptedByUserId == s.Rrhh.Id && a.EvaluatedUserId == s.Employee.Id);
    }

    [Fact]
    public async Task Handle_WhenAcceptedAgain_UpdatesTheChosenSource()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);
        await CreateSut(db).Handle(Request(s, AcceptedAnswerSource.Autoevaluacion), CancellationToken.None);

        await CreateSut(db).Handle(Request(s, AcceptedAnswerSource.Superior), CancellationToken.None);

        var stored = await db.DiscrepancyAcceptances.AsNoTracking().SingleAsync();
        stored.AcceptedSource.Should().Be(AcceptedAnswerSource.Superior);
    }
}
