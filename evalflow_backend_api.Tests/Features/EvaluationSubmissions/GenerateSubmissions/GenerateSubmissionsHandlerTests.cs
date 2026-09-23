using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationSubmissions.GenerateSubmissions;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.EvaluationSubmissions.GenerateSubmissions;

public class GenerateSubmissionsHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IEmailService> _emailService = new();

    private GenerateSubmissionsHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object, _emailService.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GenerateSubmissionsRecord(1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownCycle_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GenerateSubmissionsRecord(999), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithCycleBelongingToAnotherTenant_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        db.Companies.Add(otherCompany);
        await db.SaveChangesAsync();

        var cycle = new EvaluationCycle { Nombre = "Cycle", FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(30), EmpresaID = otherCompany.Id, Empresa = otherCompany };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GenerateSubmissionsRecord(cycle.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithAssignedUsersAndNoSuperior_CreatesSelfSubmissionOnly()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
        db.Companies.Add(company);
        db.Users.Add(employee);
        await db.SaveChangesAsync();

        var question = new Question { Texto = "How are you?", Tipo = QuestionType.Escala1a5, Orden = 1 };
        var template = new Template
        {
            Titulo = "Template",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
            UsuariosAsignados = [employee],
            Preguntas = [question],
        };
        var cycle = new EvaluationCycle
        {
            Nombre = "Cycle",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
            Templates = [template],
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GenerateSubmissionsRecord(cycle.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);

        var submissions = await db.EvaluationSubmissions.Include(s => s.Answers).ToListAsync();
        submissions.Should().ContainSingle();
        submissions[0].EvaluatedUserId.Should().Be(employee.Id);
        submissions[0].RespondentUserId.Should().Be(employee.Id);
        submissions[0].Answers.Should().ContainSingle();

        _emailService.Verify(e => e.SendEmailAsync(employee.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAssignedUserHavingSuperior_CreatesSelfAndManagerSubmissions()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var manager = TestDataFactory.CreateUser(company, email: "manager@example.com");
        db.Companies.Add(company);
        db.Users.Add(manager);
        await db.SaveChangesAsync();

        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
        employee.SuperiorId = manager.Id;
        db.Users.Add(employee);
        await db.SaveChangesAsync();

        var question = new Question { Texto = "How are you?", Tipo = QuestionType.Escala1a5, Orden = 1 };
        var template = new Template
        {
            Titulo = "Template",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
            UsuariosAsignados = [employee],
            Preguntas = [question],
        };
        var cycle = new EvaluationCycle
        {
            Nombre = "Cycle",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
            Templates = [template],
            TipoEvaluación = EvaluationType.Evaluacion360,
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GenerateSubmissionsRecord(cycle.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);

        var submissions = await db.EvaluationSubmissions.ToListAsync();
        submissions.Should().HaveCount(2);
        submissions.Should().ContainSingle(s => s.EvaluatedUserId == employee.Id && s.RespondentUserId == employee.Id);
        submissions.Should().ContainSingle(s => s.EvaluatedUserId == employee.Id && s.RespondentUserId == manager.Id);
    }

    [Fact]
    public async Task Handle_WithAutoType_NeverCreatesManagerSubmissionEvenWithSuperior()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var manager = TestDataFactory.CreateUser(company, email: "manager@example.com");
        db.Companies.Add(company);
        db.Users.Add(manager);
        await db.SaveChangesAsync();

        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
        employee.SuperiorId = manager.Id;
        db.Users.Add(employee);
        await db.SaveChangesAsync();

        var question = new Question { Texto = "How are you?", Tipo = QuestionType.Escala1a5, Orden = 1 };
        var template = new Template
        {
            Titulo = "Template",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
            UsuariosAsignados = [employee],
            Preguntas = [question],
        };
        var cycle = new EvaluationCycle
        {
            Nombre = "Cycle",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
            Templates = [template],
            TipoEvaluación = EvaluationType.Auto,
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        await sut.Handle(new GenerateSubmissionsRecord(cycle.Id), CancellationToken.None);

        var submissions = await db.EvaluationSubmissions.ToListAsync();
        submissions.Should().ContainSingle();
        submissions[0].RespondentUserId.Should().Be(employee.Id);
        submissions[0].EvaluatedUserId.Should().Be(employee.Id);
    }

    [Fact]
    public async Task Handle_With180Type_OnlyCreatesManagerSubmissionNoSelfEvaluation()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var manager = TestDataFactory.CreateUser(company, email: "manager@example.com");
        db.Companies.Add(company);
        db.Users.Add(manager);
        await db.SaveChangesAsync();

        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
        employee.SuperiorId = manager.Id;
        db.Users.Add(employee);
        await db.SaveChangesAsync();

        var question = new Question { Texto = "How are you?", Tipo = QuestionType.Escala1a5, Orden = 1 };
        var template = new Template
        {
            Titulo = "Template",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
            UsuariosAsignados = [employee],
            Preguntas = [question],
        };
        var cycle = new EvaluationCycle
        {
            Nombre = "Cycle",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
            Templates = [template],
            TipoEvaluación = EvaluationType.Evaluacion180,
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        await sut.Handle(new GenerateSubmissionsRecord(cycle.Id), CancellationToken.None);

        var submissions = await db.EvaluationSubmissions.ToListAsync();
        submissions.Should().ContainSingle();
        submissions[0].RespondentUserId.Should().Be(manager.Id);
        submissions[0].EvaluatedUserId.Should().Be(employee.Id);
    }

    [Fact]
    public async Task Handle_WhenSubmissionsAlreadyExist_DoesNotDuplicateOrSendEmails()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
        db.Companies.Add(company);
        db.Users.Add(employee);
        await db.SaveChangesAsync();

        var question = new Question { Texto = "How are you?", Tipo = QuestionType.Escala1a5, Orden = 1 };
        var template = new Template
        {
            Titulo = "Template",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
            UsuariosAsignados = [employee],
            Preguntas = [question],
        };
        var cycle = new EvaluationCycle
        {
            Nombre = "Cycle",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            Empresa = company,
            Templates = [template],
        };
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        db.EvaluationSubmissions.Add(new EvaluationSubmission
        {
            EvaluationCycleId = cycle.Id,
            TemplateId = template.Id,
            EvaluatedUserId = employee.Id,
            RespondentUserId = employee.Id,
        });
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GenerateSubmissionsRecord(cycle.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        (await db.EvaluationSubmissions.CountAsync()).Should().Be(1);
        _emailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
