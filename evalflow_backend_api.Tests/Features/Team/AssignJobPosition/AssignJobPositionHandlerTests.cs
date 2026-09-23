using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Team.AssignJobPosition;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Team.AssignJobPosition;

public class AssignJobPositionHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private AssignJobPositionHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new AssignJobPositionRecord(1, null), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new AssignJobPositionRecord(1, null), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
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

        var result = await sut.Handle(new AssignJobPositionRecord(999, null), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithJobPositionFromAnotherCompany_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
        var otherCompanyPosition = new JobPosition { Nombre = "Analista", EmpresaID = otherCompany.Id, Empresa = otherCompany };
        db.Companies.AddRange(company, otherCompany);
        db.Users.Add(employee);
        await db.SaveChangesAsync();
        db.JobPositions.Add(otherCompanyPosition);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new AssignJobPositionRecord(employee.Id, otherCompanyPosition.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithNullJobPositionId_RemovesPositionAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var position = new JobPosition { Nombre = "Analista", EmpresaID = company.Id, Empresa = company };
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
        db.Companies.Add(company);
        db.JobPositions.Add(position);
        db.Users.Add(employee);
        await db.SaveChangesAsync();
        employee.CargoId = position.Id;
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new AssignJobPositionRecord(employee.Id, null), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var refreshed = await db.Users.SingleAsync(u => u.Id == employee.Id);
        refreshed.CargoId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithValidJobPosition_AssignsPositionAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var position = new JobPosition { Nombre = "Analista", EmpresaID = company.Id, Empresa = company };
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
        db.Companies.Add(company);
        db.JobPositions.Add(position);
        db.Users.Add(employee);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new AssignJobPositionRecord(employee.Id, position.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var refreshed = await db.Users.SingleAsync(u => u.Id == employee.Id);
        refreshed.CargoId.Should().Be(position.Id);
    }
}
