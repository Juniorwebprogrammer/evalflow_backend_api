using evalflow_backend_api.Features.Companies.UpdateCompaniesInformation;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Companies.UpdateCompaniesInformation;

public class UpdateCompaniesInformationHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private UpdateCompaniesInformationHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateCompaniesInformationRecord("New Name", "logo.png", "#fff", "B12345678", "Address", "Tech"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenCompanyDoesNotExist_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateCompaniesInformationRecord("New Name", "logo.png", "#fff", "B12345678", "Address", "Tech"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithValidTenant_UpdatesCompanyAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1", nombre: "Old Name");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateCompaniesInformationRecord("New Name", "logo.png", "#123456", "B87654321", "New Address", "Tech"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var updated = await db.Companies.SingleAsync();
        updated.Nombre.Should().Be("New Name");
        updated.LogoUrl.Should().Be("logo.png");
        updated.Colors.Should().Be("#123456");
        updated.Cif.Should().Be("B87654321");
        updated.DireccionFiscal.Should().Be("New Address");
        updated.Sector.Should().Be("Tech");
    }
}
