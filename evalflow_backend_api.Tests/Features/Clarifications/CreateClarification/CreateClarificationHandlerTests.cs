using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.Clarifications.ClarificationsDto;
using evalflow_backend_api.Features.Clarifications.CreateClarification;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Frontend;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace evalflow_backend_api.Tests.Features.Clarifications.CreateClarification;

public class CreateClarificationHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly FakeEmailService _emailService = new();

    public CreateClarificationHandlerTests()
    {
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");
    }

    private CreateClarificationHandler CreateSut(AppDbContext dbContext, IEmailService? emailService = null) =>
        new(dbContext, _currentUser.Object, emailService ?? _emailService,
            Options.Create(new FrontendSettings { BaseUrl = "http://frontend.test" }),
            NullLogger<CreateClarificationHandler>.Instance);

    private async Task<ClarificationScenario> SeedAsync(AppDbContext db, EvaluationType tipo = EvaluationType.Evaluacion360,
        bool selfCompleted = true, bool managerCompleted = true, string tenant = "tenant-1")
    {
        var s = await ClarificationTestData.SeedAsync(db, tenant, tipo, selfCompleted, managerCompleted);
        _currentUser.Setup(c => c.GetUserId()).Returns(s.Rrhh.Id.ToString());
        return s;
    }

    private static CreateClarificationRecord Request(ClarificationScenario s, string mensaje = "Explica la nota", int? questionId = -1) =>
        new(s.Cycle.Id, s.Employee.Id, s.Template.Id, questionId == -1 ? s.Question.Id : questionId, mensaje);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var result = await CreateSut(db).Handle(new CreateClarificationRecord(1, 1, 1, null, "x"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status401Unauthorized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithBlankMessage_ReturnsBadRequest(string mensaje)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);

        var result = await CreateSut(db).Handle(Request(s, mensaje), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
        db.ClarificationRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithTooLongMessage_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);

        var result = await CreateSut(db).Handle(Request(s, new string('a', 1001)), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithCycleFromAnotherTenant_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db, tenant: "tenant-2");

        var result = await CreateSut(db).Handle(Request(s), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData(EvaluationType.Auto)]
    [InlineData(EvaluationType.Evaluacion180)]
    public async Task Handle_WithNon360Cycle_ReturnsBadRequest(EvaluationType tipo)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db, tipo);

        var result = await CreateSut(db).Handle(Request(s), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Handle_WithIncompleteEvaluations_ReturnsBadRequest(bool selfCompleted, bool managerCompleted)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db, selfCompleted: selfCompleted, managerCompleted: managerCompleted);

        var result = await CreateSut(db).Handle(Request(s), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
        _emailService.SentEmails.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithQuestionFromAnotherTemplate_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);

        var result = await CreateSut(db).Handle(Request(s, questionId: 9999), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithValidRequest_PersistsClarificationAndEmailsBothParticipants()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);

        var result = await CreateSut(db).Handle(Request(s, "  ¿Por qué <b>1</b> estrella?  "), CancellationToken.None);

        var dto = result.Should().BeOfType<Created<ClarificationDto>>().Subject.Value!;
        dto.ManagerUserId.Should().Be(s.Manager.Id);
        dto.QuestionText.Should().Be("Cumple plazos");
        dto.Estado.Should().Be(ClarificationStatus.Pendiente);

        var stored = await db.ClarificationRequests.SingleAsync();
        stored.Mensaje.Should().Be("¿Por qué <b>1</b> estrella?");
        stored.RequestedByUserId.Should().Be(s.Rrhh.Id);
        stored.EvaluatedUserId.Should().Be(s.Employee.Id);
        stored.ManagerUserId.Should().Be(s.Manager.Id);

        _emailService.SentEmails.Select(e => e.To).Should().BeEquivalentTo([s.Employee.Email, s.Manager.Email]);
        _emailService.SentEmails.Should().AllSatisfy(e =>
        {
            e.HtmlBody.Should().Contain("http://frontend.test/dashboard/solicitudes-informacion");
            e.HtmlBody.Should().Contain("&lt;b&gt;1&lt;/b&gt;");
            e.HtmlBody.Should().NotContain("<b>1</b>");
        });
    }

    [Fact]
    public async Task Handle_WithoutQuestion_CreatesGeneralClarification()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);

        var result = await CreateSut(db).Handle(Request(s, questionId: null), CancellationToken.None);

        var dto = result.Should().BeOfType<Created<ClarificationDto>>().Subject.Value!;
        dto.QuestionId.Should().BeNull();
        dto.QuestionText.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenEmailFails_StillCreatesClarification()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var s = await SeedAsync(db);
        var failingEmail = new Mock<IEmailService>();
        failingEmail
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("brevo down"));

        var result = await CreateSut(db, failingEmail.Object).Handle(Request(s), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status201Created);
        db.ClarificationRequests.Should().ContainSingle();
    }
}
