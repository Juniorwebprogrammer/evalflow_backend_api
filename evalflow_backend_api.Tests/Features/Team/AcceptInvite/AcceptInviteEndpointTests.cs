using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Team.AcceptInvite;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Team.AcceptInvite;

public class AcceptInviteEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_AcceptInvite_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync("/team/accept-invite", new AcceptInviteRecord("some-token", "Ana", "Perez", "Password123"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_AcceptInvite_WithInvalidToken_ReturnsBadRequest()
    {
        // The endpoint allows anonymous access (no JWT required), so the plain Client (API key only) suffices.
        var response = await Client.PostAsJsonAsync("/team/accept-invite", new AcceptInviteRecord("does-not-exist", "Ana", "Perez", "Password123"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_AcceptInvite_WithValidToken_ActivatesUserAndReturnsOk()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "invited@example.com", emailVerificado: false);
        user.TokenInvitacion = "valid-token";
        user.TokenInvitacionExpiracion = DateTime.UtcNow.AddDays(7);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/team/accept-invite", new AcceptInviteRecord("valid-token", "Ana", "Perez", "Password123"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        var refreshed = await verifyDb.Users.SingleAsync(u => u.Id == user.Id);
        refreshed.EmailVerificado.Should().BeTrue();
        refreshed.TokenInvitacion.Should().BeNull();
    }
}
