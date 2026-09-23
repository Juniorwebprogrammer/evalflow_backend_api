using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationSubmissions.EvaluationSubmissionsDto;
using evalflow_backend_api.Features.EvaluationSubmissions.GetSubmissionById;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationSubmissions.GetSubmissionById;

public class GetSubmissionByIdHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetSubmissionByIdHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetSubmissionByIdRecord(1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownSubmission_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns("1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetSubmissionByIdRecord(999), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithSubmissionBelongingToAnotherRespondent_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");
        var otherUser = TestDataFactory.CreateUser(company, email: "other@example.com");
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(respondent, otherUser, evaluated);
        await db.SaveChangesAsync();

        var question = new Question { Texto = "Q1", Tipo = QuestionType.Escala1a5, Orden = 1 };
        var template = new Template { Titulo = "Template", FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(30), EmpresaID = company.Id, Empresa = company, Preguntas = [question] };
        var cycle = new EvaluationCycle { Nombre = "Cycle", FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(30), EmpresaID = company.Id, Empresa = company };
        db.Templates.Add(template);
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        var submission = new EvaluationSubmission { Cycle = cycle, Template = template, EvaluatedUser = evaluated, RespondentUser = respondent };
        db.EvaluationSubmissions.Add(submission);
        await db.SaveChangesAsync();

        // Someone who isn't the respondent tries to view it (e.g. the evaluated user themselves, or an unrelated user).
        _currentUser.Setup(c => c.GetUserId()).Returns(otherUser.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetSubmissionByIdRecord(submission.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithOwnSubmission_ReturnsMappedDetailDto()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(respondent, evaluated);
        await db.SaveChangesAsync();

        var question1 = new Question { Texto = "Second", Tipo = QuestionType.Escala1a5, Orden = 2 };
        var question2 = new Question { Texto = "First", Tipo = QuestionType.Estrellas, Orden = 1 };
        var template = new Template
        {
            Titulo = "Template",
            Descripcion = "Desc",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
            Preguntas = [question1, question2],
        };
        var cycleFechaFin = DateTime.UtcNow.AddDays(30);
        var cycle = new EvaluationCycle { Nombre = "Cycle", FechaInicio = DateTime.UtcNow, FechaFin = cycleFechaFin, EmpresaID = company.Id, Empresa = company };
        db.Templates.Add(template);
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        var submission = new EvaluationSubmission { Cycle = cycle, Template = template, EvaluatedUser = evaluated, RespondentUser = respondent, IsCompleted = false };
        db.EvaluationSubmissions.Add(submission);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(respondent.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetSubmissionByIdRecord(submission.Id), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<SubmissionDetailDto>>().Subject;
        ok.Value!.SubmissionId.Should().Be(submission.Id);
        ok.Value.CycleName.Should().Be("Cycle");
        ok.Value.EvaluatedUserName.Should().Be($"{evaluated.Nombre} {evaluated.Apellidos}");
        ok.Value.TemplateTitle.Should().Be("Template");
        ok.Value.FechaFinCiclo.Should().Be(cycleFechaFin);
        ok.Value.Questions.Should().HaveCount(2);
        ok.Value.Questions[0].Texto.Should().Be("First");
        ok.Value.Questions[1].Texto.Should().Be("Second");
    }
}
