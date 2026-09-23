using evalflow_backend_api.Features.Profile.UpdateProfileInformation;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Profile.UpdateProfileInformation;

public class UpdateProfileInformationHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private UpdateProfileInformationHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    [Fact]
    public async Task Handle_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateProfileInformationRecord("Jane", "Doe"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithUnknownUserId_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns("999");

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateProfileInformationRecord("Jane", "Doe"), CancellationToken.None);

        result.Should().BeOfType<NotFound<string>>();
    }

    [Fact]
    public async Task Handle_WithValidUser_UpdatesNameAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());

        var sut = CreateSut(db);

        var result = await sut.Handle(new UpdateProfileInformationRecord("Jane", "Doe"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);

        var updated = await db.Users.FindAsync(user.Id);
        updated!.Nombre.Should().Be("Jane");
        updated.Apellidos.Should().Be("Doe");
        updated.FechaActualizacion.Should().NotBeNull();
    }
}
