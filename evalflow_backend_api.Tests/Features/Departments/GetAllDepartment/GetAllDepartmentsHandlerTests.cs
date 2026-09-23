using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Departments.GetAllDepartment;
using evalflow_backend_api.Features.Departments.GetAllDepartments;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace evalflow_backend_api.Tests.Features.Departments.GetAllDepartment;

public class GetAllDepartmentsHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetAllDepartmentsHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetAllDepartmentsRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("unknown-tenant");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetAllDepartmentsRecord(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithValidTenant_ReturnsOnlyOwnDepartmentsOrderedByName()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        db.Companies.AddRange(company, otherCompany);
        await db.SaveChangesAsync();

        db.Departments.AddRange(
            new Department { Nombre = "Zeta", EmpresaID = company.Id },
            new Department { Nombre = "Alpha", EmpresaID = company.Id },
            new Department { Nombre = "Other Co Dept", EmpresaID = otherCompany.Id }
        );
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetAllDepartmentsRecord(), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<DepartmentSummaryDto>>>().Subject;
        ok.Value!.Should().HaveCount(2);
        ok.Value!.Select(d => d.Nombre).Should().ContainInOrder("Alpha", "Zeta");
    }
}
