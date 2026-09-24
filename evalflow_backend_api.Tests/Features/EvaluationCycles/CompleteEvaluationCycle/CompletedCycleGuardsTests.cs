using evalflow_backend_api.Features.Clarifications.CreateClarification;
using evalflow_backend_api.Features.EvaluationCycles.DeleteEvaluationCycle;
using evalflow_backend_api.Features.EvaluationSubmissions.DeleteSubmission;
using evalflow_backend_api.Features.EvaluationSubmissions.GenerateSubmissions;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Frontend;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using evalflow_backend_api.Tests.Features.EvaluationResults;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationCycles.CompleteEvaluationCycle;

public class CompletedCycleGuardsTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public CompletedCycleGuardsTests()
    {
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");
    }

    private async Task<(AppDbContext Db, ResultsScenario Scenario)> SeedCompletedAsync()
    {
        var db = InMemoryDbContextFactory.Create();
        var s = await ResultsTestData.SeedAsync(db, raw => $"enc:{raw}");
        s.Cycle.FechaCompletado = DateTime.UtcNow;
        s.Cycle.Activo = false;
        await db.SaveChangesAsync();
        _currentUser.Setup(c => c.GetUserId()).Returns(s.Rrhh.Id.ToString());
        return (db, s);
    }

    [Fact]
    public async Task GenerateSubmissions_OnCompletedCycle_ReturnsConflict()
    {
        var (db, s) = await SeedCompletedAsync();
        await using var _ = db;
        var sut = new GenerateSubmissionsHandler(db, _currentUser.Object, new FakeEmailService(),
            Options.Create(new FrontendSettings { BaseUrl = "http://frontend.test" }));

        var result = await sut.Handle(new GenerateSubmissionsRecord(s.Cycle.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task DeleteCycle_OnCompletedCycle_ReturnsBadRequest()
    {
        var (db, s) = await SeedCompletedAsync();
        await using var _ = db;

        var result = await new DeleteEvaluationCycleHandler(db, _currentUser.Object)
            .Handle(new DeleteEvaluationCycleRecord(s.Cycle.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
        db.EvaluationCycles.Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteSubmission_OnCompletedCycle_ReturnsConflict()
    {
        var (db, s) = await SeedCompletedAsync();
        await using var _ = db;

        var result = await new DeleteSubmissionHandler(db, _currentUser.Object)
            .Handle(new DeleteSubmissionRecord(s.SelfSubmission.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task CreateClarification_OnCompletedCycle_ReturnsConflict()
    {
        var (db, s) = await SeedCompletedAsync();
        await using var _ = db;
        var sut = new CreateClarificationHandler(db, _currentUser.Object, new FakeEmailService(),
            Options.Create(new FrontendSettings { BaseUrl = "http://frontend.test" }), NullLogger<CreateClarificationHandler>.Instance);

        var result = await sut.Handle(new CreateClarificationRecord(s.Cycle.Id, s.Employee.Id, s.Template.Id, null, "¿Por qué?"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status409Conflict);
    }
}
