using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.Clarifications.ClarificationsDto;
using evalflow_backend_api.Features.Clarifications.RespondClarification;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Clarifications.RespondClarification;

public class RespondClarificationHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IEncryptionService> _encryptionService = new();

    public RespondClarificationHandlerTests()
    {
        _encryptionService.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(plain => $"enc:{plain}");
    }

    private RespondClarificationHandler CreateSut(AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object, _encryptionService.Object);

    private void SignInAs(int userId) => _currentUser.Setup(c => c.GetUserId()).Returns(userId.ToString());

    [Fact]
    public async Task Handle_WithoutUserClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var result = await CreateSut(db).Handle(new RespondClarificationRecord(1, "x"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Handle_WithBlankResponse_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ClarificationTestData.SeedAsync(db);
        var clarification = await ClarificationTestData.AddClarificationAsync(db, s);
        SignInAs(s.Employee.Id);

        var result = await CreateSut(db).Handle(new RespondClarificationRecord(clarification.Id, "  "), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotParticipant_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ClarificationTestData.SeedAsync(db);
        var clarification = await ClarificationTestData.AddClarificationAsync(db, s);
        SignInAs(s.Rrhh.Id);

        var result = await CreateSut(db).Handle(new RespondClarificationRecord(clarification.Id, "Hola"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WhenEvaluatedResponds_EncryptsResponseAndMarksPartial()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ClarificationTestData.SeedAsync(db);
        var clarification = await ClarificationTestData.AddClarificationAsync(db, s);
        SignInAs(s.Employee.Id);

        var result = await CreateSut(db).Handle(new RespondClarificationRecord(clarification.Id, "  Tuve mucha carga  "), CancellationToken.None);

        var dto = result.Should().BeOfType<Ok<MyClarificationDto>>().Subject.Value!;
        dto.MyRole.Should().Be(ClarificationParticipant.Evaluado);
        dto.MyResponse.Should().Be("Tuve mucha carga");

        var stored = await db.ClarificationRequests.AsNoTracking().SingleAsync();
        stored.EvaluatedResponseEncrypted.Should().Be("enc:Tuve mucha carga");
        stored.EvaluatedRespondedAt.Should().NotBeNull();
        stored.ManagerResponseEncrypted.Should().BeNull();
        stored.Estado.Should().Be(ClarificationStatus.Parcial);
    }

    [Fact]
    public async Task Handle_WhenBothParticipantsRespond_MarksResponded()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ClarificationTestData.SeedAsync(db);
        var clarification = await ClarificationTestData.AddClarificationAsync(db, s, evaluatedResponse: "enc:ya respondí");
        SignInAs(s.Manager.Id);

        var result = await CreateSut(db).Handle(new RespondClarificationRecord(clarification.Id, "Faltó a dos entregas"), CancellationToken.None);

        var dto = result.Should().BeOfType<Ok<MyClarificationDto>>().Subject.Value!;
        dto.MyRole.Should().Be(ClarificationParticipant.Evaluador);

        var stored = await db.ClarificationRequests.AsNoTracking().SingleAsync();
        stored.ManagerResponseEncrypted.Should().Be("enc:Faltó a dos entregas");
        stored.Estado.Should().Be(ClarificationStatus.Respondida);
    }

    [Fact]
    public async Task Handle_WhenAlreadyResponded_ReturnsConflict()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await ClarificationTestData.SeedAsync(db);
        var clarification = await ClarificationTestData.AddClarificationAsync(db, s, evaluatedResponse: "enc:primera");
        SignInAs(s.Employee.Id);

        var result = await CreateSut(db).Handle(new RespondClarificationRecord(clarification.Id, "segunda"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status409Conflict);
        (await db.ClarificationRequests.AsNoTracking().SingleAsync()).EvaluatedResponseEncrypted.Should().Be("enc:primera");
    }
}
