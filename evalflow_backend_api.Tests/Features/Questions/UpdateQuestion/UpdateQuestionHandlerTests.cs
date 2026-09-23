using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.Questions.UpdateQuestion;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.Questions.UpdateQuestion;

public class UpdateQuestionHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private UpdateQuestionHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateQuestionRecord(1, 1, "Updated text", "General", QuestionType.Escala1a5, null, 1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownQuestion_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateQuestionRecord(1, 999, "Updated text", "General", QuestionType.Escala1a5, null, 1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithQuestionFromAnotherTenant_ReturnsNotFound()
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

        var otherQuestion = new Question
        {
            Texto = "Other tenant question",
            Tipo = QuestionType.Escala1a5,
            Orden = 1,
            TemplateId = otherTemplate.Id,
        };
        db.Questions.Add(otherQuestion);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateQuestionRecord(otherTemplate.Id, otherQuestion.Id, "Updated text", "General", QuestionType.Escala1a5, null, 1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithMismatchedTemplateId_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var templateA = new Template
        {
            Titulo = "Template A",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(1),
            EmpresaID = company.Id,
        };
        var templateB = new Template
        {
            Titulo = "Template B",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(1),
            EmpresaID = company.Id,
        };
        db.Templates.AddRange(templateA, templateB);
        await db.SaveChangesAsync();

        var question = new Question
        {
            Texto = "Belongs to template A",
            Tipo = QuestionType.Escala1a5,
            Orden = 1,
            TemplateId = templateA.Id,
        };
        db.Questions.Add(question);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateQuestionRecord(templateB.Id, question.Id, "Updated text", "General", QuestionType.Escala1a5, null, 1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithValidData_UpdatesQuestionAndReturnsOk()
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

        var question = new Question
        {
            Texto = "Old text",
            Tipo = QuestionType.Escala1a5,
            Topic = "General",
            Orden = 1,
            TemplateId = template.Id,
        };
        db.Questions.Add(question);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateQuestionRecord(template.Id, question.Id, "New text", "Teamwork", QuestionType.Estrellas, null, 2), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var updated = await db.Questions.FindAsync(question.Id);
        updated!.Texto.Should().Be("New text");
        updated.Topic.Should().Be("Teamwork");
        updated.Tipo.Should().Be(QuestionType.Estrellas);
        updated.Orden.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ChangingTypeAwayFromSeleccion_ClearsOpciones()
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

        var question = new Question
        {
            Texto = "Do you agree?",
            Tipo = QuestionType.Seleccion,
            Topic = "General",
            Opciones = new List<string> { "Yes", "No" },
            Orden = 1,
            TemplateId = template.Id,
        };
        db.Questions.Add(question);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateQuestionRecord(template.Id, question.Id, "Describe your experience", "General", QuestionType.Escala1a5, new List<string> { "Yes", "No" }, 1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var updated = await db.Questions.FindAsync(question.Id);
        updated!.Opciones.Should().BeNull();
    }
}
