using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.EvaluationSubmissions.DeleteSubmission;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationSubmissions.DeleteSubmission;

public class DeleteSubmissionHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private DeleteSubmissionHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteSubmissionRecord(1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownSubmission_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteSubmissionRecord(999), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithSubmissionBelongingToAnotherTenant_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var ownCompany = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        db.Companies.AddRange(ownCompany, otherCompany);
        await db.SaveChangesAsync();

        var (submission, _) = SeedSubmission(db, otherCompany);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteSubmissionRecord(submission.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
        (await db.EvaluationSubmissions.FindAsync(submission.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithSubmissionInOwnTenant_DeletesSubmissionAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var (submission, _) = SeedSubmission(db, company);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteSubmissionRecord(submission.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        (await db.EvaluationSubmissions.FindAsync(submission.Id)).Should().BeNull();
    }

    private static (EvaluationSubmission submission, User respondent) SeedSubmission(
        evalflow_backend_api.Infrastructure.Database.AppDbContext db, Company company)
    {
        var evaluated = TestDataFactory.CreateUser(company, email: $"evaluated-{Guid.NewGuid():N}@example.com");
        var respondent = TestDataFactory.CreateUser(company, email: $"respondent-{Guid.NewGuid():N}@example.com");
        db.Users.AddRange(evaluated, respondent);

        var template = new Template
        {
            Titulo = "Template",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
        };

        var cycle = new EvaluationCycle
        {
            Nombre = "Cycle",
            Activo = true,
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
        };

        var submission = new EvaluationSubmission
        {
            Cycle = cycle,
            Template = template,
            EvaluatedUser = evaluated,
            RespondentUser = respondent,
            IsCompleted = false,
        };

        db.EvaluationSubmissions.Add(submission);
        return (submission, respondent);
    }
}
