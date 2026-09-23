using evalflow_backend_api.Features.Departments.CreateDepartment;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Departments.CreateDepartment;

public class CreateDepartmentHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private CreateDepartmentHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateDepartmentRecord("Engineering", null), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateDepartmentRecord("Engineering", null), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithValidTenant_CreatesDepartmentAndReturnsCreated()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new CreateDepartmentRecord("Engineering", "Builds the product"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status201Created);
        var department = await db.Departments.SingleAsync();
        department.Nombre.Should().Be("Engineering");
        department.Descripcion.Should().Be("Builds the product");
        department.EmpresaID.Should().Be(company.Id);
    }
}
