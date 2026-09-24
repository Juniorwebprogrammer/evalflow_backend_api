using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.EvaluationResults.EvaluationResultsDto;
using evalflow_backend_api.Features.EvaluationResults.GetMyEvaluationResults;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationResults.GetMyEvaluationResults;

public class GetMyEvaluationResultsHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    [Fact]
    public async Task Handle_WithoutUserClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();

        var result = await new GetMyEvaluationResultsHandler(db, _currentUser.Object).Handle(new GetMyEvaluationResultsRecord(), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheCallersResults()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ResultsTestData.SeedAsync(db, raw => $"enc:{raw}");
        db.EvaluationResults.AddRange(
            new EvaluationResult { EvaluationCycleId = s.Cycle.Id, TemplateId = s.Template.Id, EvaluatedUserId = s.Employee.Id, CompletedByUserId = s.Rrhh.Id, AverageFinal = 4.5, EncryptedSnapshot = "x" },
            new EvaluationResult { EvaluationCycleId = s.Cycle.Id, TemplateId = s.Template.Id, EvaluatedUserId = s.Manager.Id, CompletedByUserId = s.Rrhh.Id, EncryptedSnapshot = "x" });
        await db.SaveChangesAsync();
        _currentUser.Setup(c => c.GetUserId()).Returns(s.Employee.Id.ToString());

        var result = await new GetMyEvaluationResultsHandler(db, _currentUser.Object).Handle(new GetMyEvaluationResultsRecord(), CancellationToken.None);

        var item = result.Should().BeOfType<Ok<List<EvaluationResultSummaryDto>>>().Subject.Value!.Should().ContainSingle().Subject;
        item.EvaluatedUserId.Should().Be(s.Employee.Id);
        item.CycleName.Should().Be("Q1");
        item.TemplateTitle.Should().Be("Desempeño");
        item.AverageFinal.Should().Be(4.5);
    }
}
