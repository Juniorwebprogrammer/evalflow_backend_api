using evalflow_backend_api.Features.Team.AssignSuperior;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Team.AssignSuperior;

public class AssignSuperiorHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private AssignSuperiorHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new AssignSuperiorRecord(1, 2), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new AssignSuperiorRecord(1, 2), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenUserIdEqualsSuperiorId_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new AssignSuperiorRecord(5, 5), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithEmployeeNotInCompany_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new AssignSuperiorRecord(999, 998), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithSuperiorFromAnotherCompany_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
        var otherCompanySuperior = TestDataFactory.CreateUser(otherCompany, email: "superior@other.com");
        db.Companies.AddRange(company, otherCompany);
        db.Users.AddRange(employee, otherCompanySuperior);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new AssignSuperiorRecord(employee.Id, otherCompanySuperior.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithCircularReference_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var employeeA = TestDataFactory.CreateUser(company, email: "a@example.com");
        var employeeB = TestDataFactory.CreateUser(company, email: "b@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(employeeA, employeeB);
        await db.SaveChangesAsync();

        // B's superior is A. Now try to make A's superior be B -> circular loop.
        employeeB.SuperiorId = employeeA.Id;
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new AssignSuperiorRecord(employeeA.Id, employeeB.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithNullSuperiorId_RemovesSuperiorAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var superior = TestDataFactory.CreateUser(company, email: "superior@example.com");
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(superior, employee);
        await db.SaveChangesAsync();
        employee.SuperiorId = superior.Id;
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new AssignSuperiorRecord(employee.Id, null), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var refreshed = await db.Users.SingleAsync(u => u.Id == employee.Id);
        refreshed.SuperiorId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithValidSuperior_AssignsSuperiorAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var superior = TestDataFactory.CreateUser(company, email: "superior@example.com");
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(superior, employee);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new AssignSuperiorRecord(employee.Id, superior.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var refreshed = await db.Users.SingleAsync(u => u.Id == employee.Id);
        refreshed.SuperiorId.Should().Be(superior.Id);
    }
}
