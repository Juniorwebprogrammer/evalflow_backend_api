using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.Questions.GetQuestionsByTemplate;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using QuestionDtoType = evalflow_backend_api.Features.Questions.QuestionDto.QuestionDto;

namespace evalflow_backend_api.Tests.Features.Questions.GetQuestionsByTemplate;

public class GetQuestionsByTemplateHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetQuestionsByTemplateHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetQuestionsByTemplateRecord(1), CancellationToken.None);

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

        var result = await sut.Handle(new GetQuestionsByTemplateRecord(999), CancellationToken.None);

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

        var result = await sut.Handle(new GetQuestionsByTemplateRecord(otherTemplate.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithValidTemplate_ReturnsQuestionsOrderedByOrden()
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

        var second = new Question { Texto = "Second question", Tipo = QuestionType.Escala1a5, Orden = 2, TemplateId = template.Id };
        var first = new Question { Texto = "First question", Tipo = QuestionType.Estrellas, Orden = 1, TemplateId = template.Id };
        db.Questions.AddRange(second, first);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetQuestionsByTemplateRecord(template.Id), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<QuestionDtoType>>>().Subject;
        ok.Value.Should().HaveCount(2);
        ok.Value!.Select(q => q.Texto).Should().ContainInOrder("First question", "Second question");
    }
}
