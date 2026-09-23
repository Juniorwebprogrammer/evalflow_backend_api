using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationSubmissions.EvaluationSubmissionsDto;
using evalflow_backend_api.Features.EvaluationSubmissions.GetMyCompletedSubmissions;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationSubmissions.GetMyCompletedSubmissions;

public class GetMyCompletedSubmissionsHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetMyCompletedSubmissionsHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetMyCompletedSubmissionsRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithNonNumericUserIdClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns("not-a-number");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetMyCompletedSubmissionsRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyCallersCompletedSubmissions_OrderedByMostRecent()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");
        var otherRespondent = TestDataFactory.CreateUser(company, email: "other@example.com");
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(respondent, otherRespondent, evaluated);
        await db.SaveChangesAsync();

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
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
        };
        db.Templates.Add(template);
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        var olderCompleted = new EvaluationSubmission
        {
            Cycle = cycle, Template = template, EvaluatedUser = evaluated, RespondentUser = respondent,
            IsCompleted = true, SubmittedAt = DateTime.UtcNow.AddDays(-2),
        };
        var newerCompleted = new EvaluationSubmission
        {
            Cycle = cycle, Template = template, EvaluatedUser = evaluated, RespondentUser = respondent,
            IsCompleted = true, SubmittedAt = DateTime.UtcNow.AddDays(-1),
        };
        var pendingForSameUser = new EvaluationSubmission
        {
            Cycle = cycle, Template = template, EvaluatedUser = evaluated, RespondentUser = respondent,
            IsCompleted = false,
        };
        var completedForOtherUser = new EvaluationSubmission
        {
            Cycle = cycle, Template = template, EvaluatedUser = evaluated, RespondentUser = otherRespondent,
            IsCompleted = true, SubmittedAt = DateTime.UtcNow,
        };
        db.EvaluationSubmissions.AddRange(olderCompleted, newerCompleted, pendingForSameUser, completedForOtherUser);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(respondent.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetMyCompletedSubmissionsRecord(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<PendingSubmissionDto>>>().Subject;
        ok.Value.Should().HaveCount(2);
        ok.Value![0].SubmissionId.Should().Be(newerCompleted.Id);
        ok.Value[1].SubmissionId.Should().Be(olderCompleted.Id);
    }
}
