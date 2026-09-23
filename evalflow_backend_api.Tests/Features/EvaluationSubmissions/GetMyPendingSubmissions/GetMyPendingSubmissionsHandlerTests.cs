using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationSubmissions.EvaluationSubmissionsDto;
using evalflow_backend_api.Features.EvaluationSubmissions.GetMyPendingSubmissions;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationSubmissions.GetMyPendingSubmissions;

public class GetMyPendingSubmissionsHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetMyPendingSubmissionsHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetMyPendingSubmissionsRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithNonNumericUserIdClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns("nope");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetMyPendingSubmissionsRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyOwnPendingSubmissionsOnActiveCycles_OrderedByCycleEndDate()
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
        var activeCycleEndingSoon = new EvaluationCycle { Nombre = "Soon", Activo = true, FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(5), EmpresaID = company.Id, Empresa = company };
        var activeCycleEndingLater = new EvaluationCycle { Nombre = "Later", Activo = true, FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(20), EmpresaID = company.Id, Empresa = company };
        var inactiveCycle = new EvaluationCycle { Nombre = "Inactive", Activo = false, FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(1), EmpresaID = company.Id, Empresa = company };
        db.Templates.Add(template);
        db.EvaluationCycles.AddRange(activeCycleEndingSoon, activeCycleEndingLater, inactiveCycle);
        await db.SaveChangesAsync();

        var pendingSoon = new EvaluationSubmission { Cycle = activeCycleEndingSoon, Template = template, EvaluatedUser = evaluated, RespondentUser = respondent, IsCompleted = false };
        var pendingLater = new EvaluationSubmission { Cycle = activeCycleEndingLater, Template = template, EvaluatedUser = evaluated, RespondentUser = respondent, IsCompleted = false };
        var completedOnActiveCycle = new EvaluationSubmission { Cycle = activeCycleEndingSoon, Template = template, EvaluatedUser = evaluated, RespondentUser = respondent, IsCompleted = true };
        var pendingOnInactiveCycle = new EvaluationSubmission { Cycle = inactiveCycle, Template = template, EvaluatedUser = evaluated, RespondentUser = respondent, IsCompleted = false };
        var pendingForOtherUser = new EvaluationSubmission { Cycle = activeCycleEndingSoon, Template = template, EvaluatedUser = evaluated, RespondentUser = otherRespondent, IsCompleted = false };
        db.EvaluationSubmissions.AddRange(pendingSoon, pendingLater, completedOnActiveCycle, pendingOnInactiveCycle, pendingForOtherUser);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(respondent.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetMyPendingSubmissionsRecord(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<PendingSubmissionDto>>>().Subject;
        ok.Value.Should().HaveCount(2);
        ok.Value![0].SubmissionId.Should().Be(pendingSoon.Id);
        ok.Value[1].SubmissionId.Should().Be(pendingLater.Id);
    }
}
