using evalflow_backend_api.Features.Clarifications.ClarificationsDto;
using evalflow_backend_api.Features.Clarifications.GetCycleClarifications;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace evalflow_backend_api.Tests.Features.Clarifications.GetCycleClarifications;

public class GetCycleClarificationsHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IEncryptionService> _encryptionService = new();

    public GetCycleClarificationsHandlerTests()
    {
        _encryptionService
            .Setup(e => e.Decrypt(It.IsAny<string>()))
            .Returns<string>(cipher => cipher.StartsWith("enc:") ? cipher[4..] : throw new FormatException("bad payload"));
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");
    }

    private GetCycleClarificationsHandler CreateSut(AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object, _encryptionService.Object, NullLogger<GetCycleClarificationsHandler>.Instance);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var result = await CreateSut(db).Handle(new GetCycleClarificationsRecord(1, null), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Handle_WithCycleFromAnotherTenant_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ClarificationTestData.SeedAsync(db, tenant: "tenant-2");

        var result = await CreateSut(db).Handle(new GetCycleClarificationsRecord(s.Cycle.Id, null), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_DecryptsBothResponses()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ClarificationTestData.SeedAsync(db);
        await ClarificationTestData.AddClarificationAsync(db, s, evaluatedResponse: "enc:mi motivo", managerResponse: "corrupto");

        var result = await CreateSut(db).Handle(new GetCycleClarificationsRecord(s.Cycle.Id, null), CancellationToken.None);

        var item = result.Should().BeOfType<Ok<List<ClarificationDto>>>().Subject.Value!.Should().ContainSingle().Subject;
        item.EvaluatedResponse.Should().Be("mi motivo");
        item.ManagerResponse.Should().BeNull();
        item.EvaluatedUserName.Should().Be("Enrique User");
        item.ManagerName.Should().Be("Marta User");
        item.RequestedByName.Should().Be("Rosa User");
    }

    [Fact]
    public async Task Handle_WithEvaluatedUserFilter_ReturnsOnlyThatEmployee()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ClarificationTestData.SeedAsync(db);
        await ClarificationTestData.AddClarificationAsync(db, s);

        var result = await CreateSut(db).Handle(new GetCycleClarificationsRecord(s.Cycle.Id, s.Manager.Id), CancellationToken.None);

        result.Should().BeOfType<Ok<List<ClarificationDto>>>().Subject.Value!.Should().BeEmpty();
    }
}
