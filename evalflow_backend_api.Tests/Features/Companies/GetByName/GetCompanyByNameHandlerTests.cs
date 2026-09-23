using evalflow_backend_api.Features.Companies.GetByName;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Companies.GetByName;

public class GetCompanyByNameHandlerTests
{
    private static GetCompanyByNameHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext);

    [Fact]
    public async Task Handle_WhenCompanyExists_ReturnsCompanyResponse()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(nombre: "Acme Corp", identificationId: "tenant-1");
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetCompanyByNameQuery("Acme Corp"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.IdentificationId.Should().Be("tenant-1");
    }

    [Fact]
    public async Task Handle_WhenCompanyDoesNotExist_ReturnsNull()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(new GetCompanyByNameQuery("Unknown Corp"), CancellationToken.None);

        result.Should().BeNull();
    }
}
