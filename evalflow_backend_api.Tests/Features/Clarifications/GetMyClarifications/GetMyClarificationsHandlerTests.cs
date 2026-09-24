using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.Clarifications.ClarificationsDto;
using evalflow_backend_api.Features.Clarifications.GetMyClarifications;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace evalflow_backend_api.Tests.Features.Clarifications.GetMyClarifications;

public class GetMyClarificationsHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IEncryptionService> _encryptionService = new();

    public GetMyClarificationsHandlerTests()
    {
        _encryptionService
            .Setup(e => e.Decrypt(It.IsAny<string>()))
            .Returns<string>(cipher => cipher.StartsWith("enc:") ? cipher[4..] : throw new FormatException("bad payload"));
    }

    private GetMyClarificationsHandler CreateSut(AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object, _encryptionService.Object, NullLogger<GetMyClarificationsHandler>.Instance);

    private async Task<List<MyClarificationDto>> ListAsAsync(AppDbContext db, int userId)
    {
        _currentUser.Setup(c => c.GetUserId()).Returns(userId.ToString());
        var result = await CreateSut(db).Handle(new GetMyClarificationsRecord(), CancellationToken.None);
        return result.Should().BeOfType<Ok<List<MyClarificationDto>>>().Subject.Value!;
    }

    [Fact]
    public async Task Handle_WithoutUserClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var result = await CreateSut(db).Handle(new GetMyClarificationsRecord(), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Handle_ForEvaluated_ReturnsOnlyOwnResponse()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ClarificationTestData.SeedAsync(db);
        await ClarificationTestData.AddClarificationAsync(db, s, evaluatedResponse: "enc:mi motivo", managerResponse: "enc:motivo del jefe");

        var list = await ListAsAsync(db, s.Employee.Id);

        var item = list.Should().ContainSingle().Subject;
        item.MyRole.Should().Be(ClarificationParticipant.Evaluado);
        item.MyResponse.Should().Be("mi motivo");
        item.QuestionText.Should().Be("Cumple plazos");
        item.CycleName.Should().Be("Q1");
    }

    [Fact]
    public async Task Handle_ForManager_ReturnsEvaluatorRoleAndPendingResponse()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ClarificationTestData.SeedAsync(db);
        await ClarificationTestData.AddClarificationAsync(db, s, evaluatedResponse: "enc:mi motivo");

        var list = await ListAsAsync(db, s.Manager.Id);

        var item = list.Should().ContainSingle().Subject;
        item.MyRole.Should().Be(ClarificationParticipant.Evaluador);
        item.MyResponse.Should().BeNull();
        item.MyRespondedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForUnrelatedUser_ReturnsEmpty()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ClarificationTestData.SeedAsync(db);
        await ClarificationTestData.AddClarificationAsync(db, s);

        var list = await ListAsAsync(db, s.Rrhh.Id);

        list.Should().BeEmpty();
    }
}
