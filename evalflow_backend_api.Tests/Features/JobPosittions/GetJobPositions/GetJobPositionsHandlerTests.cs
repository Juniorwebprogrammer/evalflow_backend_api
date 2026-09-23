using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.JobPosittions.JobPositionDto;
using evalflow_backend_api.Features.JobPositions.GetJobPositions;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.JobPosittions.GetJobPositions;

public class GetJobPositionsHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetJobPositionsHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetJobPositionsRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("unknown-tenant");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetJobPositionsRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithValidTenant_ReturnsOnlyOwnJobPositionsOrderedByName()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        db.Companies.AddRange(company, otherCompany);
        await db.SaveChangesAsync();

        db.JobPositions.AddRange(
            new JobPosition { Nombre = "Zebra Handler", EmpresaID = company.Id },
            new JobPosition { Nombre = "Analyst", EmpresaID = company.Id },
            new JobPosition { Nombre = "Other Co Position", EmpresaID = otherCompany.Id }
        );
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetJobPositionsRecord(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<JobPositionDto>>>().Subject;
        ok.Value!.Should().HaveCount(2);
        ok.Value!.Select(p => p.Nombre).Should().ContainInOrder("Analyst", "Zebra Handler");
    }
}
