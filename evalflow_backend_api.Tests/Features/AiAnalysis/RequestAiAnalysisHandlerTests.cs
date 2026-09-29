using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.AiAnalysis.AiAnalysisDto;
using evalflow_backend_api.Features.AiAnalysis.RequestAiAnalysis;
using evalflow_backend_api.Features.EvaluationComparisons.EvaluationComparisonsDto;
using evalflow_backend_api.Features.EvaluationComparisons.GetCycleComparisons;
using evalflow_backend_api.Infrastructure.AI;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Plans;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using evalflow_backend_api.Tests.Common;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace evalflow_backend_api.Tests.Features.AiAnalysis;

public class RequestAiAnalysisHandlerTests
{
    private const string Tenant = "tenant-ai";
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IEncryptionService> _encryption = new();
    private readonly Mock<ISender> _sender = new();
    private readonly AiSettings _settings = new() { Enabled = true, ApiKey = "key" };
    private List<EmployeeComparisonDto> _comparisons = [AiAnalysisTestData.Employee(10, 20)];

    public RequestAiAnalysisHandlerTests()
    {
        _currentUser.Setup(c => c.GetIdentificationId()).Returns(Tenant);
        _currentUser.Setup(c => c.GetUserId()).Returns("1");
        _encryption.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(p => $"enc:{p}");
        _encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(c => c[4..]);
        _sender
            .Setup(s => s.Send(It.IsAny<GetCycleComparisonsRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => TypedResults.Ok(new CycleComparisonsDto(1, "Q1", EvaluationType.Evaluacion360, false, null, 0, _comparisons)));
    }

    private RequestAiAnalysisHandler CreateSut(AppDbContext db) => new(db, _currentUser.Object, new PlanLimitService(db),
        _encryption.Object, _sender.Object, new AiAnalysisQueueSignal(), Options.Create(_settings),
        NullLogger<RequestAiAnalysisHandler>.Instance);

    private static async Task<EvaluationCycle> SeedAsync(AppDbContext db, int planId = PlanCatalog.Growth, string tenant = Tenant)
    {
        var company = TestDataFactory.CreateCompany(identificationId: tenant, planId: planId);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var cycle = new EvaluationCycle
        {
            Nombre = "Q1", FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id, TipoEvaluación = EvaluationType.Evaluacion360,
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();
        return cycle;
    }

    private static Task<IResult> Request(RequestAiAnalysisHandler sut, int cycleId, bool force = false) =>
        sut.Handle(new RequestAiAnalysisRecord(cycleId, null, null, force), CancellationToken.None);

    private static RequestAiAnalysisResponse Payload(IResult result) =>
        result.Should().BeOfType<Accepted<RequestAiAnalysisResponse>>().Subject.Value!;

    [Fact]
    public async Task Handle_StarterPlan_ReturnsPlanFeatureError()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var cycle = await SeedAsync(db, PlanCatalog.Starter);

        var result = await Request(CreateSut(db), cycle.Id);

        var json = result.Should().BeOfType<JsonHttpResult<PlanFeatureError>>().Subject;
        json.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        json.Value!.Code.Should().Be(RequestAiAnalysisHandler.FeatureNotAvailableCode);
        db.AiEvaluationAnalyses.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AiDisabled_Returns503()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var cycle = await SeedAsync(db);
        _settings.Enabled = false;

        var result = await Request(CreateSut(db), cycle.Id);

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task Handle_CycleOfAnotherTenant_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedAsync(db);
        var other = await SeedAsync(db, tenant: "other-tenant");

        var result = await Request(CreateSut(db), other.Id);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_NoCompletedEvaluations_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var cycle = await SeedAsync(db);
        _comparisons = [AiAnalysisTestData.Employee(10, 20, completed: false)];

        var result = await Request(CreateSut(db), cycle.Id);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_QueuesPseudonymisedAnalysisPerEmployee()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var cycle = await SeedAsync(db);
        _comparisons = [AiAnalysisTestData.Employee(10, 20), AiAnalysisTestData.Employee(11, 20, evaluatedName: "Ana Ruiz")];

        var payload = Payload(await Request(CreateSut(db), cycle.Id));

        payload.Created.Should().Be(2);
        payload.Analyses.Should().OnlyContain(a => a.Estado == AiAnalysisStatus.Pendiente);
        var stored = db.AiEvaluationAnalyses.ToList();
        stored.Should().HaveCount(2).And.OnlyContain(a => a.RequestedByUserId == 1 && a.InputHash.Length == 64);
        stored.Should().OnlyContain(a => !a.InputEncrypted.Contains("Enrique") && !a.InputEncrypted.Contains("Ana Ruiz")
                                         && !a.InputEncrypted.Contains("Marta"));
    }

    [Fact]
    public async Task Handle_UnchangedEvaluation_ReusesAnalysisWithoutSpendingQuota()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var cycle = await SeedAsync(db);

        await Request(CreateSut(db), cycle.Id);
        var second = Payload(await Request(CreateSut(db), cycle.Id));

        second.Created.Should().Be(0);
        second.Reused.Should().Be(1);
        db.AiEvaluationAnalyses.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ChangedEvaluation_CreatesNewAnalysis()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var cycle = await SeedAsync(db);
        await Request(CreateSut(db), cycle.Id);

        _comparisons = [AiAnalysisTestData.Employee(10, 20, questions: AiAnalysisTestData.Question(1, 3, 2))];
        var second = Payload(await Request(CreateSut(db), cycle.Id));

        second.Created.Should().Be(1);
        db.AiEvaluationAnalyses.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_Force_RegeneratesCompletedAnalysis()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var cycle = await SeedAsync(db);
        await Request(CreateSut(db), cycle.Id);
        var first = db.AiEvaluationAnalyses.Single();
        first.Estado = AiAnalysisStatus.Completado;
        await db.SaveChangesAsync();

        var second = Payload(await Request(CreateSut(db), cycle.Id, force: true));

        second.Created.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MonthlyQuotaReached_ReturnsPlanLimitError()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var cycle = await SeedAsync(db);
        var quota = PlanCatalog.Get(PlanCatalog.Growth).MaxAiAnalysesPerMonth!.Value;
        db.AiEvaluationAnalyses.AddRange(Enumerable.Range(0, quota).Select(i => new AiEvaluationAnalysis
        {
            EmpresaID = cycle.EmpresaID, EvaluationCycleId = cycle.Id, TemplateId = 99, EvaluatedUserId = 1000 + i,
            RequestedByUserId = 1, InputHash = $"h{i}", Estado = AiAnalysisStatus.Completado,
        }));
        await db.SaveChangesAsync();

        var result = await Request(CreateSut(db), cycle.Id);

        var json = result.Should().BeOfType<JsonHttpResult<PlanLimitError>>().Subject;
        json.Value!.Code.Should().Be(PlanLimitService.LimitReachedCode);
        json.Value.Resource.Should().Be(nameof(PlanLimit.AiAnalysesPerMonth));
    }

    [Fact]
    public async Task Handle_FailedAnalysesDoNotCountTowardsQuota()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var cycle = await SeedAsync(db);
        var quota = PlanCatalog.Get(PlanCatalog.Growth).MaxAiAnalysesPerMonth!.Value;
        db.AiEvaluationAnalyses.AddRange(Enumerable.Range(0, quota).Select(i => new AiEvaluationAnalysis
        {
            EmpresaID = cycle.EmpresaID, EvaluationCycleId = cycle.Id, TemplateId = 99, EvaluatedUserId = 1000 + i,
            RequestedByUserId = 1, InputHash = $"h{i}", Estado = AiAnalysisStatus.Error,
        }));
        await db.SaveChangesAsync();

        var payload = Payload(await Request(CreateSut(db), cycle.Id));

        payload.Created.Should().Be(1);
    }
}
