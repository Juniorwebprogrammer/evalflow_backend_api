using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.Questions.CreateQuestion;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Questions.CreateQuestion;

public class CreateQuestionHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private CreateQuestionHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateQuestionRecord(1, "Question text", "General", QuestionType.Escala1a5, null, 1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTemplate_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateQuestionRecord(999, "Question text", "General", QuestionType.Escala1a5, null, 1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithTemplateFromAnotherTenant_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var ownCompany = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        db.Companies.AddRange(ownCompany, otherCompany);
        await db.SaveChangesAsync();

        var otherTemplate = new Template
        {
            Titulo = "Other tenant template",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(1),
            EmpresaID = otherCompany.Id,
        };
        db.Templates.Add(otherTemplate);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateQuestionRecord(otherTemplate.Id, "Question text", "General", QuestionType.Escala1a5, null, 1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithValidData_CreatesQuestionAndReturnsCreated()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var template = new Template
        {
            Titulo = "Peer Review",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(1),
            EmpresaID = company.Id,
        };
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateQuestionRecord(template.Id, "How would you rate your teammate?", "Teamwork", QuestionType.Escala1a5, null, 1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status201Created);
        var question = await db.Questions.SingleAsync();
        question.Texto.Should().Be("How would you rate your teammate?");
        question.Tipo.Should().Be(QuestionType.Escala1a5);
        question.Orden.Should().Be(1);
        question.TemplateId.Should().Be(template.Id);

        question.Topic.Should().Be("Teamwork");
    }

    [Fact]
    public async Task Handle_WithSeleccionType_KeepsOpciones()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var template = new Template
        {
            Titulo = "Peer Review",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(1),
            EmpresaID = company.Id,
        };
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);
        var opciones = new List<string> { "Yes", "No" };

        var result = await sut.Handle(new CreateQuestionRecord(template.Id, "Do you agree?", "General", QuestionType.Seleccion, opciones, 1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status201Created);
        var question = await db.Questions.SingleAsync();
        question.Opciones.Should().BeEquivalentTo(opciones);
    }

    [Fact]
    public async Task Handle_WithNonSeleccionTypeAndOpciones_ClearsOpciones()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var template = new Template
        {
            Titulo = "Peer Review",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(1),
            EmpresaID = company.Id,
        };
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);
        var opciones = new List<string> { "Yes", "No" };

        var result = await sut.Handle(new CreateQuestionRecord(template.Id, "Describe your experience", "General", QuestionType.Escala1a5, opciones, 1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status201Created);
        var question = await db.Questions.SingleAsync();
        question.Opciones.Should().BeNull();
    }
}
