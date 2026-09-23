using evalflow_backend_api.Features.JobPosittions.CreateJobPosition;
using evalflow_backend_api.Features.JobPositions.CreateJobPosition;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.JobPosittions.CreateJobPosition;

public class CreateJobPositionHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private CreateJobPositionHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateJobPositionRecord("Engineer", null), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("unknown-tenant");

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateJobPositionRecord("Engineer", null), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithValidTenant_CreatesJobPositionAndReturnsCreated()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateJobPositionRecord("Engineer", "Builds software"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status201Created);
        var position = await db.JobPositions.SingleAsync();
        position.Nombre.Should().Be("Engineer");
        position.Descripcion.Should().Be("Builds software");
        position.EmpresaID.Should().Be(company.Id);
    }
}
