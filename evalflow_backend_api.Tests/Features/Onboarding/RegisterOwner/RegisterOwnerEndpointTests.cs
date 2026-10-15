using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Onboarding.RegisterOwner;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Onboarding.RegisterOwner;

public class RegisterOwnerEndpointTests : IntegrationTestBase
{
    private RegisterOwnerRecord ValidRequest(string? planId = "starter") => new(
        VexxenAccessToken: Factory.FakeVexxen.AddUser(FakeVexxenClient.User("vx-ada", "owner@example.com"), planId, "trialing"),
        CompanyNombre: "Acme Corp",
        CompanyColors: "#FFFFFF",
        Cif: "B12345678",
        DireccionFiscal: "Calle Falsa 123",
        Sector: "Tech");

    [Fact]
    public async Task Post_RegisterOwner_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync("/Onboarding/register-owner", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_RegisterOwner_WithoutPlan_ReturnsConflict()
    {
        var response = await Client.PostAsJsonAsync("/Onboarding/register-owner", ValidRequest(planId: null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("NO_PLAN");
    }

    [Fact]
    public async Task Post_RegisterOwner_WithValidRequest_CreatesCompanyAndOwnerWithoutSendingEmails()
    {
        var response = await Client.PostAsJsonAsync("/Onboarding/register-owner", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<RegisterOwnerResponse>();
        payload!.Jwt.Should().NotBeNullOrEmpty();
        payload.PlanStatus.Should().Be("trialing");

        await using var verifyDb = Factory.CreateDbContext();
        var createdUser = await verifyDb.Users.Include(u => u.Empresa).SingleAsync(u => u.Email == "owner@example.com");
        createdUser.Empresa!.Nombre.Should().Be("acme corp");
        createdUser.VexxenUserId.Should().Be("vx-ada");

        // Vexxen already verified the email: no welcome/verification email from EvalFlow.
        Factory.FakeEmailService.SentEmails.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_RegisterOwner_WithValidRequest_SeedsDefault180_360AndAutoTemplates()
    {
        var response = await Client.PostAsJsonAsync("/Onboarding/register-owner", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Templates.CountAsync()).Should().Be(3);
    }
}
