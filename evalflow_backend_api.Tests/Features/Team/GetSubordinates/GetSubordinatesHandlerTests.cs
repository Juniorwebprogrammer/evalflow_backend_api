using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Team.GetSubordinates;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.Team.GetSubordinates;

public class GetSubordinatesHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetSubordinatesHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetSubordinatesRecord(1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetSubordinatesRecord(1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithNoSubordinates_ReturnsOkWithEmptyList()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var superior = TestDataFactory.CreateUser(company, email: "superior@example.com");
        db.Companies.Add(company);
        db.Users.Add(superior);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetSubordinatesRecord(superior.Id), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<SubordinateDto>>>().Subject;
        ok.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithSubordinates_ReturnsOnlyThoseReportingToGivenSuperior()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var position = new JobPosition { Nombre = "Analista", EmpresaID = company.Id, Empresa = company };
        var superior = TestDataFactory.CreateUser(company, email: "superior@example.com");
        var subordinate = TestDataFactory.CreateUser(company, email: "subordinate@example.com");
        var unrelatedEmployee = TestDataFactory.CreateUser(company, email: "unrelated@example.com");

        db.Companies.Add(company);
        db.JobPositions.Add(position);
        db.Users.AddRange(superior, subordinate, unrelatedEmployee);
        await db.SaveChangesAsync();

        subordinate.SuperiorId = superior.Id;
        subordinate.CargoId = position.Id;
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetSubordinatesRecord(superior.Id), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<SubordinateDto>>>().Subject;
        ok.Value.Should().ContainSingle();
        ok.Value![0].Email.Should().Be("subordinate@example.com");
    }
}
