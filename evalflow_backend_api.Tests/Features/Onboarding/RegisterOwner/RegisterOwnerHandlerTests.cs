using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Onboarding.RegisterOwner;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Jwt;
using evalflow_backend_api.Infrastructure.Vexxen;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace evalflow_backend_api.Tests.Features.Onboarding.RegisterOwner;

public class RegisterOwnerHandlerTests
{
    private readonly Mock<IJwtProvider> _jwtProvider = new();
    private readonly FakeVexxenClient _vexxen = new();

    public RegisterOwnerHandlerTests()
    {
        _jwtProvider.Setup(j => j.GenerateJwt(It.IsAny<User>(), It.IsAny<Company>())).Returns("fake-jwt");
    }

    private RegisterOwnerHandler CreateSut(AppDbContext dbContext) =>
        new(dbContext, _jwtProvider.Object, _vexxen,
            Options.Create(new VexxenSettings { BaseUrl = "http://vexxen.test", ClientId = "c", ApiKey = "k" }));

    private static RegisterOwnerRecord Request(string accessToken) => new(
        VexxenAccessToken: accessToken,
        CompanyNombre: "Acme Corp",
        CompanyColors: "#FFFFFF",
        Cif: "B12345678",
        DireccionFiscal: "Calle Falsa 123",
        Sector: "Tech");

    private string Owner(string? planId = "professional", string status = "trialing") =>
        _vexxen.AddUser(FakeVexxenClient.User("vx-ada", "Ada@Example.com"), planId, status);

    [Fact]
    public async Task Handle_WithAPlanInVexxen_CreatesTheCompanyWithThatPlanAndTheOwnerFromVexxen()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var trialEnds = DateTime.UtcNow.AddDays(14);
        var token = Owner();
        _vexxen.SetPlan("vx-ada", "professional", "trialing", trialEndsAt: trialEnds);

        var result = await CreateSut(db).Handle(Request(token), CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<RegisterOwnerResponse>>().Subject;
        ok.Value!.Jwt.Should().Be("fake-jwt");
        ok.Value.PlanId.Should().Be(PlanCatalog.Professional);
        ok.Value.PlanStatus.Should().Be(PlanStatuses.Trialing);

        var owner = await db.Users.Include(u => u.Empresa).SingleAsync();
        owner.VexxenUserId.Should().Be("vx-ada");
        owner.Email.Should().Be("ada@example.com");
        owner.Nombre.Should().Be("Ada");
        owner.Apellidos.Should().Be("Lovelace");
        owner.Rol.Should().Be(AppRoles.Owner);
        owner.Empresa!.Nombre.Should().Be("acme corp");
        owner.Empresa.BillingVexxenUserId.Should().Be("vx-ada");
        owner.Empresa.PlanId.Should().Be(PlanCatalog.Professional);
        owner.Empresa.TrialEndsAt.Should().BeCloseTo(trialEnds, TimeSpan.FromSeconds(1));
        owner.Empresa.PlanSyncedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null, "none")]
    [InlineData("starter", "none")]
    [InlineData("pro", "active")]
    public async Task Handle_WithoutAnEvalFlowPlan_ReturnsNoPlanWithTheCatalogAndCreatesNothing(string? planId, string status)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var token = Owner(planId, status);

        var result = await CreateSut(db).Handle(Request(token), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status409Conflict);
        var body = System.Text.Json.JsonSerializer.Serialize(((IValueHttpResult)result).Value);
        body.Should().Contain("NO_PLAN").And.Contain("http://vexxen.test/dashboard/catalog?product=evaluation-flow");
        (await db.Companies.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenTheEmailIsAlreadyInEvalFlow_ReturnsConflict()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        db.Users.Add(TestDataFactory.CreateUser(company, email: "ada@example.com", vexxenUserId: null));
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(Request(Owner()), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status409Conflict);
        (await db.Companies.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheSubscriptionAlreadyPaysForAnotherCompany_ReturnsConflict()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.Companies.Add(TestDataFactory.CreateCompany(billingVexxenUserId: "vx-ada"));
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(Request(Owner()), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status409Conflict);
        (await db.Companies.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithAnInvalidVexxenToken_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();

        var result = await CreateSut(db).Handle(Request("not-a-token"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Handle_WhenVexxenIsDown_ReturnsServiceUnavailable()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var token = Owner();
        _vexxen.Down = true;

        var result = await CreateSut(db).Handle(Request(token), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status503ServiceUnavailable);
        (await db.Companies.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithoutACompanyName_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();

        var result = await CreateSut(db).Handle(Request(Owner()) with { CompanyNombre = " " }, CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithValidRequest_SeedsDefaultTemplatesForTheNewCompany()
    {
        await using var db = InMemoryDbContextFactory.Create();

        await CreateSut(db).Handle(Request(Owner()), CancellationToken.None);

        var createdCompany = await db.Companies.SingleAsync();
        var templateTitles = await db.Templates.Where(t => t.EmpresaID == createdCompany.Id).Select(t => t.Titulo).ToListAsync();

        templateTitles.Should().HaveCount(3);
        templateTitles.Should().Contain("Standard 180° Template (Manager Only)");
        templateTitles.Should().Contain("Comprehensive 360° Template");
        templateTitles.Should().Contain("Self-Assessment Template");
    }
}
