using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Team.GetEmployees;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.Team.GetEmployees;

public class GetEmployeeHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetEmployeeHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetEmployeeRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsOkWithEmptyList()
    {
        // Note: unlike sibling Team handlers (AssignDepartment, AssignSuperior, GetSubordinates,
        // ToggleUserStatus), GetEmployeeHandler never re-validates that the tenant claim maps to
        // an existing Company row - it just filters users by IdentificationId. An unknown tenant
        // therefore yields an empty (but 200 OK) list rather than Unauthorized.
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("does-not-exist");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetEmployeeRecord(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<GetEmployeeResponse>>>().Subject;
        ok.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithValidTenant_ReturnsOnlyEmployeesForThatTenantOrderedByNewestFirst()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        var position = new JobPosition { Nombre = "Analista", EmpresaID = company.Id, Empresa = company };

        var olderEmployee = TestDataFactory.CreateUser(company, email: "older@example.com");
        var newerEmployee = TestDataFactory.CreateUser(company, email: "newer@example.com");
        var otherCompanyEmployee = TestDataFactory.CreateUser(otherCompany, email: "outsider@example.com");

        olderEmployee.FechaCreacion = DateTime.UtcNow.AddDays(-2);
        newerEmployee.FechaCreacion = DateTime.UtcNow.AddDays(-1);

        db.Companies.AddRange(company, otherCompany);
        db.JobPositions.Add(position);
        db.Users.AddRange(olderEmployee, newerEmployee, otherCompanyEmployee);
        await db.SaveChangesAsync();

        olderEmployee.CargoId = position.Id;
        newerEmployee.CargoId = position.Id;
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetEmployeeRecord(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<GetEmployeeResponse>>>().Subject;
        ok.Value.Should().HaveCount(2);
        ok.Value!.Select(e => e.Email).Should().NotContain("outsider@example.com");
        ok.Value![0].Email.Should().Be("newer@example.com");
        ok.Value![1].Email.Should().Be("older@example.com");
    }
}
