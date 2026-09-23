using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationSubmissions.EvaluationSubmissionsDto;
using evalflow_backend_api.Features.EvaluationSubmissions.SaveSubmissionAnswers;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using evalflow_backend_api.Infrastructure.SignalR;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationSubmissions.SaveSubmissionAnswers;

public class SaveSubmissionAnswersHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IEncryptionService> _encryptionService = new();
    private readonly Mock<IHubContext<DashboardHub>> _hubContext = new();
    private readonly Mock<IHubClients> _hubClients = new();
    private readonly Mock<IClientProxy> _clientProxy = new();

    public SaveSubmissionAnswersHandlerTests()
    {
        _hubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxy.Object);
        _hubContext.Setup(h => h.Clients).Returns(_hubClients.Object);
        _clientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private SaveSubmissionAnswersHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object, _encryptionService.Object, _hubContext.Object,
            NullLogger<SaveSubmissionAnswersHandler>.Instance);

    [Fact]
    public async Task Handle_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new SaveSubmissionAnswersRecord(1, []), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithSubmissionNotBelongingToCaller_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");
        var otherUser = TestDataFactory.CreateUser(company, email: "other@example.com");
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(respondent, otherUser, evaluated);
        await db.SaveChangesAsync();

        var (submission, _) = SeedSubmissionWithOneQuestion(db, company, evaluated, respondent);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(otherUser.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new SaveSubmissionAnswersRecord(submission.Id, [new AnswerInputDto(1, "answer")]), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WhenAlreadyCompleted_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(respondent, evaluated);
        await db.SaveChangesAsync();

        var (submission, question) = SeedSubmissionWithOneQuestion(db, company, evaluated, respondent);
        submission.IsCompleted = true;
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(respondent.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new SaveSubmissionAnswersRecord(submission.Id, [new AnswerInputDto(question.Id, "answer")]), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithMissingAnswers_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(respondent, evaluated);
        await db.SaveChangesAsync();

        var (submission, _) = SeedSubmissionWithOneQuestion(db, company, evaluated, respondent);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(respondent.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new SaveSubmissionAnswersRecord(submission.Id, []), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithEmptyAnswerPayload_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(respondent, evaluated);
        await db.SaveChangesAsync();

        var (submission, question) = SeedSubmissionWithOneQuestion(db, company, evaluated, respondent);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(respondent.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new SaveSubmissionAnswersRecord(submission.Id, [new AnswerInputDto(question.Id, "   ")]), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithValidAnswers_EncryptsAnswersAndMarksSubmissionCompleted()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(respondent, evaluated);
        await db.SaveChangesAsync();

        var (submission, question) = SeedSubmissionWithOneQuestion(db, company, evaluated, respondent);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(respondent.Id.ToString());
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");
        _encryptionService.Setup(e => e.Encrypt("my answer")).Returns("encrypted-value");

        var sut = CreateSut(db);

        var result = await sut.Handle(new SaveSubmissionAnswersRecord(submission.Id, [new AnswerInputDto(question.Id, "my answer")]), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);

        var updated = await db.EvaluationSubmissions.Include(s => s.Answers).SingleAsync(s => s.Id == submission.Id);
        updated.IsCompleted.Should().BeTrue();
        updated.SubmittedAt.Should().NotBeNull();
        updated.Answers.Single().EncryptedPayload.Should().Be("encrypted-value");

        _hubClients.Verify(c => c.Group("tenant-1"), Times.Once);
        _clientProxy.Verify(
            p => p.SendCoreAsync("EvaluationCompleted", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlreadyCompleted_DoesNotNotifyDashboard()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(respondent, evaluated);
        await db.SaveChangesAsync();

        var (submission, question) = SeedSubmissionWithOneQuestion(db, company, evaluated, respondent);
        submission.IsCompleted = true;
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(respondent.Id.ToString());
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        await sut.Handle(new SaveSubmissionAnswersRecord(submission.Id, [new AnswerInputDto(question.Id, "answer")]), CancellationToken.None);

        _clientProxy.Verify(
            p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDeadlineHasPassed_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(respondent, evaluated);
        await db.SaveChangesAsync();

        var (submission, question) = SeedSubmissionWithOneQuestion(
            db, company, evaluated, respondent, cycleFechaFin: DateTime.UtcNow.AddDays(-1));
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(respondent.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(
            new SaveSubmissionAnswersRecord(submission.Id, [new AnswerInputDto(question.Id, "my answer")]),
            CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }
    
    private static (EvaluationSubmission submission, Question question) SeedSubmissionWithOneQuestion(
        evalflow_backend_api.Infrastructure.Database.AppDbContext db, Company company, User evaluated, User respondent,
        DateTime? cycleFechaFin = null)
    {
        var question = new Question { Texto = "Q1", Tipo = QuestionType.Escala1a5, Orden = 1 };
        var template = new Template
        {
            Titulo = "Template",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
            Preguntas = [question],
        };
        var cycle = new EvaluationCycle
        {
            Nombre = "Cycle",
            FechaInicio = DateTime.UtcNow,
            FechaFin = cycleFechaFin ?? DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company
        };
        db.Templates.Add(template);
        db.EvaluationCycles.Add(cycle);

        var submission = new EvaluationSubmission
        {
            Cycle = cycle,
            Template = template,
            EvaluatedUser = evaluated,
            RespondentUser = respondent,
            IsCompleted = false,
            Answers = [new Answer { Question = question, EncryptedPayload = string.Empty }],
        };
        db.EvaluationSubmissions.Add(submission);

        return (submission, question);
    }
}
