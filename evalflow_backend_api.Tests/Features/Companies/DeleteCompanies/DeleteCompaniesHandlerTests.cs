using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Companies.DeleteCompanies;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Companies.DeleteCompanies;

public class DeleteCompaniesHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IPasswordHasser> _passwordHasher = new();

    private DeleteCompaniesHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object, _passwordHasher.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);
        _currentUser.Setup(c => c.GetUserId()).Returns("1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteCompaniesRecord("whatever"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteCompaniesRecord("whatever"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");
        _currentUser.Setup(c => c.GetUserId()).Returns("999");

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteCompaniesRecord("whatever"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, passwordHash: "stored-hash");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");
        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());
        _passwordHasher.Setup(p => p.Verify("wrong-password", "stored-hash")).Returns(false);

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteCompaniesRecord("wrong-password"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WhenTenantClaimDoesNotMatchAnyCompany_ReturnsNotFound()
    {
        // The company being deleted is looked up by the caller's identificationId claim, not
        // the user's own EmpresaID, so a claim that doesn't match any company yields NotFound
        // even though the user and password are perfectly valid.
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, passwordHash: "stored-hash");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("some-other-tenant");
        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());
        _passwordHasher.Setup(p => p.Verify("correct-password", "stored-hash")).Returns(true);

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteCompaniesRecord("correct-password"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithValidPassword_DeletesCompanyAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, passwordHash: "stored-hash");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");
        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());
        _passwordHasher.Setup(p => p.Verify("correct-password", "stored-hash")).Returns(true);

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteCompaniesRecord("correct-password"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        (await db.Companies.AnyAsync(c => c.IdentificationId == "tenant-1")).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_CompanyWithEvaluationData_DeletesUsersAndEvaluationData()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var owner = TestDataFactory.CreateUser(company, passwordHash: "stored-hash");
        var employee = TestDataFactory.CreateUser(company);
        db.Companies.Add(company);
        db.Users.AddRange(owner, employee);
        await db.SaveChangesAsync();

        employee.SuperiorId = owner.Id;
        var template = new Template { EmpresaID = company.Id, Titulo = "Template" };
        var cycle = new EvaluationCycle { EmpresaID = company.Id, Nombre = "Cycle" };
        db.Templates.Add(template);
        db.EvaluationCycles.Add(cycle);
        await db.SaveChangesAsync();

        db.EvaluationSubmissions.Add(new EvaluationSubmission { EvaluationCycleId = cycle.Id, TemplateId = template.Id, EvaluatedUserId = employee.Id, RespondentUserId = owner.Id });
        db.EvaluationResults.Add(new EvaluationResult { EvaluationCycleId = cycle.Id, TemplateId = template.Id, EvaluatedUserId = employee.Id, CompletedByUserId = owner.Id, EncryptedSnapshot = "snapshot" });
        db.ClarificationRequests.Add(new ClarificationRequest { EvaluationCycleId = cycle.Id, TemplateId = template.Id, EvaluatedUserId = employee.Id, ManagerUserId = owner.Id, RequestedByUserId = owner.Id, Mensaje = "message" });
        db.DiscrepancyAcceptances.Add(new DiscrepancyAcceptance { EvaluationCycleId = cycle.Id, TemplateId = template.Id, EvaluatedUserId = employee.Id, AcceptedByUserId = owner.Id });
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");
        _currentUser.Setup(c => c.GetUserId()).Returns(owner.Id.ToString());
        _passwordHasher.Setup(p => p.Verify("correct-password", "stored-hash")).Returns(true);

        var sut = CreateSut(db);

        var result = await sut.Handle(new DeleteCompaniesRecord("correct-password"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        (await db.Companies.AnyAsync()).Should().BeFalse();
        (await db.Users.AnyAsync()).Should().BeFalse();
        (await db.EvaluationSubmissions.AnyAsync()).Should().BeFalse();
        (await db.EvaluationResults.AnyAsync()).Should().BeFalse();
        (await db.ClarificationRequests.AnyAsync()).Should().BeFalse();
        (await db.DiscrepancyAcceptances.AnyAsync()).Should().BeFalse();
    }
}
