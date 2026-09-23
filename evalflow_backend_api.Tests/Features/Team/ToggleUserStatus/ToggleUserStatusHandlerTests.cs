using evalflow_backend_api.Features.Team.ToggleUserStatus;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Team.ToggleUserStatus;

public class ToggleUserStatusHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private ToggleUserStatusHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleUserStatusRecord(1, false), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleUserStatusRecord(1, false), CancellationToken.None);

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

        var result = await sut.Handle(new ToggleUserStatusRecord(999, false), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_DeactivatingEmployee_SetsActivoFalseAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com", activo: true);
        db.Companies.Add(company);
        db.Users.Add(employee);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleUserStatusRecord(employee.Id, false), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var refreshed = await db.Users.SingleAsync(u => u.Id == employee.Id);
        refreshed.Activo.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ActivatingEmployee_SetsActivoTrueAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com", activo: false);
        db.Companies.Add(company);
        db.Users.Add(employee);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleUserStatusRecord(employee.Id, true), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var refreshed = await db.Users.SingleAsync(u => u.Id == employee.Id);
        refreshed.Activo.Should().BeTrue();
    }
}
