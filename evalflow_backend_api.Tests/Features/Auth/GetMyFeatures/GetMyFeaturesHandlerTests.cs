using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Auth.GetMyFeatures;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace evalflow_backend_api.Tests.Features.Auth.GetMyFeatures;

public class GetMyFeaturesHandlerTests
{
    private GetMyFeaturesHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext);

    [Fact]
    public async Task Handle_WithEmptyRoleName_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(new GetMyFeaturesRecord(""), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownRole_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(new GetMyFeaturesRecord("Ghost"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithRoleWithoutFeatures_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.Roles.Add(new Role { Name = "Employee", Description = "Empleado estándar" });
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetMyFeaturesRecord("Employee"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithRoleWithFeatures_ReturnsOkWithFeatures()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var role = new Role { Name = "Owner", Description = "Propietario de la empresa" };
        role.Features.Add(new RoleFeature { Role = role, FeatureCode = "manage-team", Description = "Gestionar equipo" });
        role.Features.Add(new RoleFeature { Role = role, FeatureCode = "manage-billing", Description = "Gestionar facturación" });
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new GetMyFeaturesRecord("Owner"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
    }
}
