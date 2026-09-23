using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.JobPosittions.UpdateJobPosition;
using evalflow_backend_api.Features.JobPositions.ManageJobPositions;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.JobPosittions.UpdateJobPosition;

public class UpdateJobPositionHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private UpdateJobPositionHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateJobPositionRecord(1, "Engineer", null), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("unknown-tenant");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateJobPositionRecord(1, "Engineer", null), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenJobPositionBelongsToAnotherCompany_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        db.Companies.AddRange(company, otherCompany);
        await db.SaveChangesAsync();

        var position = new JobPosition { Nombre = "Engineer", EmpresaID = otherCompany.Id };
        db.JobPositions.Add(position);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateJobPositionRecord(position.Id, "Senior Engineer", null), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithOwnJobPosition_UpdatesAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var position = new JobPosition { Nombre = "Engineer", Descripcion = "Old description", EmpresaID = company.Id };
        db.JobPositions.Add(position);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateJobPositionRecord(position.Id, "Senior Engineer", "New description"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var updated = await db.JobPositions.SingleAsync();
        updated.Nombre.Should().Be("Senior Engineer");
        updated.Descripcion.Should().Be("New description");
    }
}
