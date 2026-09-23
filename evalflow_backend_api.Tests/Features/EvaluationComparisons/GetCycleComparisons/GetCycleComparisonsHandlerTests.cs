using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationComparisons.EvaluationComparisonsDto;
using evalflow_backend_api.Features.EvaluationComparisons.GetCycleComparisons;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationComparisons.GetCycleComparisons;

public class GetCycleComparisonsHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IEncryptionService> _encryptionService = new();

    public GetCycleComparisonsHandlerTests()
    {
        _encryptionService
            .Setup(e => e.Decrypt(It.IsAny<string>()))
            .Returns<string>(cipher => cipher.StartsWith("enc:") ? cipher[4..] : throw new FormatException("bad payload"));
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");
    }

    private GetCycleComparisonsHandler CreateSut(AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object, _encryptionService.Object, NullLogger<GetCycleComparisonsHandler>.Instance);

    private sealed record Scenario(EvaluationCycle Cycle, Template Template, User Employee, User Manager);

    private static async Task<Scenario> SeedAsync(AppDbContext db, EvaluationType tipo = EvaluationType.Evaluacion360, string tenant = "tenant-1")
    {
        var company = TestDataFactory.CreateCompany(identificationId: tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var cycle = new EvaluationCycle
        {
            Nombre = "Q1",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            TipoEvaluación = tipo,
        };
        var template = new Template
        {
            Titulo = "Desempeño",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
        };
        var manager = TestDataFactory.CreateUser(company, email: "manager@example.com");
        manager.Nombre = "Marta";
        db.EvaluationCycles.Add(cycle);
        db.Templates.Add(template);
        db.Users.Add(manager);
        await db.SaveChangesAsync();

        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
        employee.Nombre = "Enrique";
        employee.SuperiorId = manager.Id;
        db.Users.Add(employee);
        await db.SaveChangesAsync();

        return new Scenario(cycle, template, employee, manager);
    }

    private static async Task<Question> AddQuestionAsync(AppDbContext db, Scenario s, QuestionType tipo, int orden, string topic = "General")
    {
        var question = new Question
        {
            Texto = $"Pregunta {orden}",
            Tipo = tipo,
            Topic = topic,
            Orden = orden,
            Opciones = tipo == QuestionType.Seleccion ? ["A", "B", "C"] : null,
            TemplateId = s.Template.Id,
        };
        db.Questions.Add(question);
        await db.SaveChangesAsync();
        return question;
    }

    private static EvaluationSubmission Submission(Scenario s, User evaluated, User respondent, bool completed, Dictionary<int, string?> answers) => new()
    {
        EvaluationCycleId = s.Cycle.Id,
        TemplateId = s.Template.Id,
        EvaluatedUserId = evaluated.Id,
        RespondentUserId = respondent.Id,
        IsCompleted = completed,
        SubmittedAt = completed ? DateTime.UtcNow : null,
        Answers = answers
            .Select(a => new Answer { QuestionId = a.Key, EncryptedPayload = a.Value is null ? string.Empty : $"enc:{a.Value}" })
            .ToList(),
    };

    private async Task<EmployeeComparisonDto> CompareSingleAsync(AppDbContext db, Scenario s,
        Dictionary<int, string?> selfAnswers, Dictionary<int, string?> managerAnswers)
    {
        db.EvaluationSubmissions.AddRange(
            Submission(s, s.Employee, s.Employee, completed: true, selfAnswers),
            Submission(s, s.Employee, s.Manager, completed: true, managerAnswers));
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(new GetCycleComparisonsRecord(s.Cycle.Id, null), CancellationToken.None);

        return Payload(result).Comparisons.Should().ContainSingle().Subject;
    }

    private static CycleComparisonsDto Payload(IResult result) =>
        result.Should().BeOfType<Ok<CycleComparisonsDto>>().Subject.Value!;

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var result = await CreateSut(db).Handle(new GetCycleComparisonsRecord(1, null), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Handle_WithCycleFromAnotherTenant_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db, tenant: "tenant-2");

        var result = await CreateSut(db).Handle(new GetCycleComparisonsRecord(s.Cycle.Id, null), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData(EvaluationType.Auto)]
    [InlineData(EvaluationType.Evaluacion180)]
    public async Task Handle_WithNon360Cycle_ReturnsBadRequest(EvaluationType tipo)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db, tipo);

        var result = await CreateSut(db).Handle(new GetCycleComparisonsRecord(s.Cycle.Id, null), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Theory]
    [InlineData(QuestionType.Estrellas, "3", "3", AlignmentLevel.Alineado, GapDirection.Ninguna, 0)]
    [InlineData(QuestionType.Estrellas, "4", "3", AlignmentLevel.Leve, GapDirection.Sobrevaloracion, 1)]
    [InlineData(QuestionType.Estrellas, "3", "4", AlignmentLevel.Leve, GapDirection.Infravaloracion, -1)]
    [InlineData(QuestionType.Estrellas, "2", "4", AlignmentLevel.Desequilibrio, GapDirection.Infravaloracion, -2)]
    [InlineData(QuestionType.Estrellas, "5", "1", AlignmentLevel.Desequilibrio, GapDirection.Sobrevaloracion, 4)]
    [InlineData(QuestionType.Escala1a5, " 1 ", "4", AlignmentLevel.Desequilibrio, GapDirection.Infravaloracion, -3)]
    public async Task Handle_WithNumericAnswers_ClassifiesByGap(QuestionType tipo, string self, string manager,
        AlignmentLevel expectedLevel, GapDirection expectedDirection, int expectedGap)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);
        var question = await AddQuestionAsync(db, s, tipo, 1);

        var comparison = await CompareSingleAsync(db, s,
            new() { [question.Id] = self },
            new() { [question.Id] = manager });

        var answer = comparison.Questions.Should().ContainSingle().Subject;
        answer.Level.Should().Be(expectedLevel);
        answer.Direction.Should().Be(expectedDirection);
        answer.Gap.Should().Be(expectedGap);
        answer.SelfValue.Should().Be(int.Parse(self));
        answer.ManagerValue.Should().Be(int.Parse(manager));
    }

    [Theory]
    [InlineData(null, "3")]
    [InlineData("abc", "3")]
    public async Task Handle_WithMissingOrInvalidNumericAnswer_MarksQuestionAsNotComparable(string? self, string manager)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);
        var question = await AddQuestionAsync(db, s, QuestionType.Estrellas, 1);

        var comparison = await CompareSingleAsync(db, s,
            new() { [question.Id] = self },
            new() { [question.Id] = manager });

        var answer = comparison.Questions.Should().ContainSingle().Subject;
        answer.Level.Should().Be(AlignmentLevel.NoComparable);
        answer.Gap.Should().BeNull();
        answer.Direction.Should().Be(GapDirection.Ninguna);
    }

    [Theory]
    [InlineData("[\"A\",\"B\"]", "[\"B\",\"A\"]", AlignmentLevel.Alineado)]
    [InlineData("[\"A\"]", "[\"A\",\"C\"]", AlignmentLevel.Desequilibrio)]
    [InlineData("A", "[\"A\"]", AlignmentLevel.NoComparable)]
    public async Task Handle_WithSelectionAnswers_ComparesOptionSets(string self, string manager, AlignmentLevel expectedLevel)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);
        var question = await AddQuestionAsync(db, s, QuestionType.Seleccion, 1);

        var comparison = await CompareSingleAsync(db, s,
            new() { [question.Id] = self },
            new() { [question.Id] = manager });

        var answer = comparison.Questions.Should().ContainSingle().Subject;
        answer.Level.Should().Be(expectedLevel);
        answer.Gap.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithBothEvaluationsCompleted_ReturnsQuestionsOrderedAndSummary()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);
        var q3 = await AddQuestionAsync(db, s, QuestionType.Estrellas, 3, "Liderazgo");
        var q1 = await AddQuestionAsync(db, s, QuestionType.Estrellas, 1, "Comunicación");
        var q2 = await AddQuestionAsync(db, s, QuestionType.Escala1a5, 2, "Comunicación");
        var q4 = await AddQuestionAsync(db, s, QuestionType.Seleccion, 4);
        var q5 = await AddQuestionAsync(db, s, QuestionType.Estrellas, 5, "Liderazgo");

        var comparison = await CompareSingleAsync(db, s,
            new() { [q1.Id] = "4", [q2.Id] = "4", [q3.Id] = "2", [q4.Id] = "[\"A\"]", [q5.Id] = null },
            new() { [q1.Id] = "4", [q2.Id] = "3", [q3.Id] = "5", [q4.Id] = "[\"A\"]", [q5.Id] = "3" });

        comparison.EvaluatedUserId.Should().Be(s.Employee.Id);
        comparison.ManagerUserId.Should().Be(s.Manager.Id);
        comparison.ManagerName.Should().StartWith("Marta");
        comparison.IsComparable.Should().BeTrue();
        comparison.Questions.Select(q => q.QuestionId).Should().Equal(q1.Id, q2.Id, q3.Id, q4.Id, q5.Id);

        var summary = comparison.Summary!;
        summary.TotalQuestions.Should().Be(5);
        summary.Alineadas.Should().Be(2);
        summary.Leves.Should().Be(1);
        summary.Desequilibrios.Should().Be(1);
        summary.NoComparables.Should().Be(1);
        summary.AlignmentPercentage.Should().Be(75);
        summary.AverageSelf.Should().Be(3.33);
        summary.AverageManager.Should().Be(4);
        summary.AverageAbsoluteGap.Should().Be(1.33);
        summary.HasImbalances.Should().BeTrue();

        comparison.Topics.Select(t => t.Topic).Should().Equal("Liderazgo", "Comunicación");

        var liderazgo = comparison.Topics[0];
        liderazgo.NumericQuestions.Should().Be(1);
        liderazgo.AverageGap.Should().Be(-3);
        liderazgo.Level.Should().Be(AlignmentLevel.Desequilibrio);
        liderazgo.Direction.Should().Be(GapDirection.Infravaloracion);

        var comunicacion = comparison.Topics[1];
        comunicacion.NumericQuestions.Should().Be(2);
        comunicacion.AverageGap.Should().Be(0.5);
        comunicacion.Level.Should().Be(AlignmentLevel.Leve);
        comunicacion.Direction.Should().Be(GapDirection.Sobrevaloracion);
    }

    [Fact]
    public async Task Handle_WithManagerEvaluationPending_ReturnsNonComparableEntryWithoutAnswers()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);
        var question = await AddQuestionAsync(db, s, QuestionType.Estrellas, 1);
        db.EvaluationSubmissions.AddRange(
            Submission(s, s.Employee, s.Employee, completed: true, new() { [question.Id] = "3" }),
            Submission(s, s.Employee, s.Manager, completed: false, new() { [question.Id] = null }));
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(new GetCycleComparisonsRecord(s.Cycle.Id, null), CancellationToken.None);

        var comparison = Payload(result).Comparisons.Should().ContainSingle().Subject;
        comparison.SelfCompleted.Should().BeTrue();
        comparison.ManagerCompleted.Should().BeFalse();
        comparison.IsComparable.Should().BeFalse();
        comparison.Summary.Should().BeNull();
        comparison.Topics.Should().BeEmpty();
        comparison.Questions.Should().BeEmpty();
        _encryptionService.Verify(e => e.Decrypt(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmployeeWithoutManagerEvaluation_ReturnsNonComparableEntry()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);
        var question = await AddQuestionAsync(db, s, QuestionType.Estrellas, 1);
        db.EvaluationSubmissions.Add(Submission(s, s.Employee, s.Employee, completed: true, new() { [question.Id] = "3" }));
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(new GetCycleComparisonsRecord(s.Cycle.Id, null), CancellationToken.None);

        var comparison = Payload(result).Comparisons.Should().ContainSingle().Subject;
        comparison.ManagerUserId.Should().BeNull();
        comparison.IsComparable.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithSeveralManagerSubmissions_UsesTheCompletedOne()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);
        var question = await AddQuestionAsync(db, s, QuestionType.Estrellas, 1);
        var newManager = TestDataFactory.CreateUser(db.Companies.First(), email: "new-manager@example.com");
        db.Users.Add(newManager);
        await db.SaveChangesAsync();

        db.EvaluationSubmissions.AddRange(
            Submission(s, s.Employee, s.Employee, completed: true, new() { [question.Id] = "3" }),
            Submission(s, s.Employee, newManager, completed: false, new() { [question.Id] = null }),
            Submission(s, s.Employee, s.Manager, completed: true, new() { [question.Id] = "5" }));
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(new GetCycleComparisonsRecord(s.Cycle.Id, null), CancellationToken.None);

        var comparison = Payload(result).Comparisons.Should().ContainSingle().Subject;
        comparison.ManagerUserId.Should().Be(s.Manager.Id);
        comparison.IsComparable.Should().BeTrue();
        comparison.Questions[0].ManagerValue.Should().Be(5);
    }

    [Fact]
    public async Task Handle_WithEvaluatedUserFilter_ReturnsOnlyThatEmployee()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);
        var question = await AddQuestionAsync(db, s, QuestionType.Estrellas, 1);
        db.EvaluationSubmissions.AddRange(
            Submission(s, s.Employee, s.Employee, completed: true, new() { [question.Id] = "3" }),
            Submission(s, s.Employee, s.Manager, completed: true, new() { [question.Id] = "3" }),
            Submission(s, s.Manager, s.Manager, completed: true, new() { [question.Id] = "5" }));
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(new GetCycleComparisonsRecord(s.Cycle.Id, s.Employee.Id), CancellationToken.None);

        Payload(result).Comparisons.Should().ContainSingle(c => c.EvaluatedUserId == s.Employee.Id);
    }

    [Fact]
    public async Task Handle_WithUndecryptableAnswer_MarksOnlyThatQuestionAsNotComparable()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);
        var broken = await AddQuestionAsync(db, s, QuestionType.Estrellas, 1);
        var healthy = await AddQuestionAsync(db, s, QuestionType.Estrellas, 2);

        var self = Submission(s, s.Employee, s.Employee, completed: true, new() { [broken.Id] = "3", [healthy.Id] = "3" });
        self.Answers.First(a => a.QuestionId == broken.Id).EncryptedPayload = "corrupted";
        db.EvaluationSubmissions.AddRange(
            self,
            Submission(s, s.Employee, s.Manager, completed: true, new() { [broken.Id] = "3", [healthy.Id] = "3" }));
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(new GetCycleComparisonsRecord(s.Cycle.Id, null), CancellationToken.None);

        var comparison = Payload(result).Comparisons.Should().ContainSingle().Subject;
        comparison.Questions[0].Level.Should().Be(AlignmentLevel.NoComparable);
        comparison.Questions[1].Level.Should().Be(AlignmentLevel.Alineado);
        comparison.Summary!.NoComparables.Should().Be(1);
    }
}
