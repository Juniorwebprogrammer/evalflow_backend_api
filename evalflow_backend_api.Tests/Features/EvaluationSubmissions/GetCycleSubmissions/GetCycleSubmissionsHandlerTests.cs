using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.EvaluationSubmissions.EvaluationSubmissionsDto;
using evalflow_backend_api.Features.EvaluationSubmissions.GetCycleSubmissions;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationSubmissions.GetCycleSubmissions;

public class GetCycleSubmissionsHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetCycleSubmissionsHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetCycleSubmissionsRecord(1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Handle_WithUnknownCycle_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetCycleSubmissionsRecord(999), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithCycleFromAnotherTenant_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        db.Companies.AddRange(company, otherCompany);
        await db.SaveChangesAsync();

        var cycle = new EvaluationCycle
        {
            Nombre = "Q1",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = otherCompany.Id,
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetCycleSubmissionsRecord(cycle.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithValidCycle_ReturnsSubmissionsOrderedByRespondentName()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var cycle = new EvaluationCycle
        {
            Nombre = "Q1",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
        };
        var template = new Template
        {
            Titulo = "Peer Review",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
        };
        db.EvaluationCycles.Add(cycle);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");
        var zRespondent = TestDataFactory.CreateUser(company, email: "zed@example.com");
        var aRespondent = TestDataFactory.CreateUser(company, email: "alice@example.com");
        zRespondent.Nombre = "Zed";
        aRespondent.Nombre = "Alice";
        db.Users.AddRange(evaluated, zRespondent, aRespondent);
        await db.SaveChangesAsync();

        db.EvaluationSubmissions.AddRange(
            new EvaluationSubmission
            {
                EvaluationCycleId = cycle.Id,
                TemplateId = template.Id,
                EvaluatedUserId = evaluated.Id,
                RespondentUserId = zRespondent.Id,
                IsCompleted = false,
            },
            new EvaluationSubmission
            {
                EvaluationCycleId = cycle.Id,
                TemplateId = template.Id,
                EvaluatedUserId = evaluated.Id,
                RespondentUserId = aRespondent.Id,
                IsCompleted = true,
                SubmittedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetCycleSubmissionsRecord(cycle.Id), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<CycleSubmissionDto>>>().Subject;
        ok.Value.Should().HaveCount(2);
        ok.Value![0].RespondentUserName.Should().Be("Alice User");
        ok.Value[1].RespondentUserName.Should().Be("Zed User");
        ok.Value[0].IsCompleted.Should().BeTrue();
        ok.Value[1].IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithCycleFromSameTenantButNoSubmissions_ReturnsEmptyList()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var cycle = new EvaluationCycle
        {
            Nombre = "Q1",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetCycleSubmissionsRecord(cycle.Id), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<CycleSubmissionDto>>>().Subject;
        ok.Value.Should().BeEmpty();
    }
}
