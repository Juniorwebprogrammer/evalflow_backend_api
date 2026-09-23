using evalflow_backend_api.Features.Companies.GetByIdentificationId;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Companies.GetByIdentificationId;

public class GetCompanyByIdentificationIdHandlerTests
{
    private static GetCompanyByIdentificationIdHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext);

    [Fact]
    public async Task Handle_WhenCompanyExists_ReturnsCompanyResponse()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1", nombre: "Acme Corp");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetCompanyByIdentificationIdRecord("tenant-1"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Acme Corp");
        result.IdentificationId.Should().Be("tenant-1");
    }

    [Fact]
    public async Task Handle_WhenCompanyDoesNotExist_ReturnsNull()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(new GetCompanyByIdentificationIdRecord("unknown-tenant"), CancellationToken.None);

        result.Should().BeNull();
    }
}
