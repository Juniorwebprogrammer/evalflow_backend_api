using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Team.GetInvitation;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Team.GetInvitation;

public class GetInvitationEndpointTests : IntegrationTestBase
{
    private async Task SeedInvitationAsync(DateTime expiresAt)
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1", nombre: "acme corp");
        var invited = TestDataFactory.CreateUser(company, email: "bea@acme.com");
        invited.VexxenUserId = null;
        invited.TokenInvitacion = "invite-token";
        invited.TokenInvitacionExpiracion = expiresAt;
        await using var db = Factory.CreateDbContext();
        db.Companies.Add(company);
        db.Users.Add(invited);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ReturnsTheCompanyAndTheInvitedEmail()
    {
        await SeedInvitationAsync(DateTime.UtcNow.AddDays(2));

        var response = await Client.GetAsync("/Team/invitation/invite-token");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var invitation = (await response.Content.ReadFromJsonAsync<InvitationDto>())!;
        invitation.CompanyNombre.Should().Be("acme corp");
        invitation.Email.Should().Be("bea@acme.com");
        invitation.Expired.Should().BeFalse();
    }

    [Fact]
    public async Task SaysWhenItExpired()
    {
        await SeedInvitationAsync(DateTime.UtcNow.AddDays(-1));

        var invitation = await Client.GetFromJsonAsync<InvitationDto>("/Team/invitation/invite-token");

        invitation!.Expired.Should().BeTrue();
    }

    [Fact]
    public async Task UnknownOrAcceptedToken_ReturnsNotFound()
    {
        (await Client.GetAsync("/Team/invitation/nope")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
