using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Departments.GetDepartment;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Departments.GetDepartment;

public class GetDepartmentHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetDepartmentHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetDepartmentRecord(1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownTenant_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("unknown-tenant");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetDepartmentRecord(1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenDepartmentBelongsToAnotherCompany_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        db.Companies.AddRange(company, otherCompany);
        await db.SaveChangesAsync();

        var department = new Department { Nombre = "Engineering", EmpresaID = otherCompany.Id };
        db.Departments.Add(department);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetDepartmentRecord(department.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WhenDepartmentDoesNotExist_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetDepartmentRecord(999), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithOwnDepartment_ReturnsDepartmentDetailsWithUsers()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var department = new Department { Nombre = "Engineering", Descripcion = "Builds stuff", EmpresaID = company.Id };
        db.Departments.Add(department);
        await db.SaveChangesAsync();

        var user = TestDataFactory.CreateUser(company, email: "dev@example.com");
        user.DepartamentoId = department.Id;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetDepartmentRecord(department.Id), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<DepartmentDetailsDto>>().Subject;
        ok.Value!.Nombre.Should().Be("Engineering");
        ok.Value.Usuarios.Should().ContainSingle(u => u.Email == "dev@example.com");
    }
}
