using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Auth.Vexxen;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Auth.Vexxen;

/// <summary>POST /Auth/vexxen: the access token from the Vexxen popup becomes an EvalFlow session.</summary>
public class VexxenSignInEndpointTests : IntegrationTestBase
{
    private FakeVexxenClient Vexxen => Factory.FakeVexxen;

    private async Task<(Company Company, User User)> SeedMemberAsync(string vexxenUserId = "vx-ana", string email = "ana@acme.com",
        bool activo = true, string planStatus = PlanStatuses.Active, string? billing = "vx-owner")
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1", planStatus: planStatus, billingVexxenUserId: billing);
        var user = TestDataFactory.CreateUser(company, email: email, vexxenUserId: vexxenUserId, activo: activo, rol: AppRoles.Rrhh);
        await using var db = Factory.CreateDbContext();
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (company, user);
    }

    private Task<HttpResponseMessage> SignIn(string accessToken) =>
        Client.PostAsJsonAsync("/Auth/vexxen", new VexxenSignInRecord(accessToken));

    [Fact]
    public async Task Member_GetsAWorkingEvalFlowJwt()
    {
        await SeedMemberAsync();
        Vexxen.SetPlan("vx-owner", "professional");
        var token = Vexxen.AddUser(FakeVexxenClient.User("vx-ana", "ana@acme.com", "Ana", "Pérez"));

        var response = await SignIn(token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = (await response.Content.ReadFromJsonAsync<VexxenSessionResponse>())!;
        session.Status.Should().Be("signed_in");
        session.Rol.Should().Be(AppRoles.Rrhh);
        session.IdentificationId.Should().Be("tenant-1");
        session.RefreshToken.Should().BeNull("the frontend keeps the Vexxen refresh token it already has");

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Jwt);
        (await client.GetAsync("/Profile/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Member_GetsTheirNameAndEmailFromVexxen()
    {
        var (_, user) = await SeedMemberAsync();
        var token = Vexxen.AddUser(FakeVexxenClient.User("vx-ana", "Ana.New@Acme.com", "Ana María", "Pérez"));

        (await SignIn(token)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = Factory.CreateDbContext();
        var saved = await db.Users.SingleAsync(u => u.Id == user.Id);
        saved.Nombre.Should().Be("Ana María");
        saved.Apellidos.Should().Be("Pérez");
        saved.Email.Should().Be("ana.new@acme.com");
    }

    [Fact]
    public async Task SignIn_SyncsTheCompanyPlanFromTheBillingUser_AtMostEveryFiveMinutes()
    {
        var (company, _) = await SeedMemberAsync();
        Vexxen.SetPlan("vx-owner", "professional", "canceling", accessEndsAt: DateTime.UtcNow.AddDays(3));
        var token = Vexxen.AddUser(FakeVexxenClient.User("vx-ana", "ana@acme.com"));

        var session = (await (await SignIn(token)).Content.ReadFromJsonAsync<VexxenSessionResponse>())!;
        session.PlanStatus.Should().Be(PlanStatuses.Canceling);

        await using (var db = Factory.CreateDbContext())
        {
            var saved = await db.Companies.SingleAsync(c => c.Id == company.Id);
            saved.PlanId.Should().Be(PlanCatalog.Professional);
            saved.PlanAccessEndsAt.Should().NotBeNull();
        }

        // Signing in again right away doesn't ask Vexxen again.
        (await SignIn(token)).StatusCode.Should().Be(HttpStatusCode.OK);
        Vexxen.AccessCalls.Should().Be(1);
    }

    [Fact]
    public async Task InvitedPerson_IsLinkedByTheirVexxenEmail_AndTheInvitationIsUsedUp()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var invited = TestDataFactory.CreateUser(company, email: "bea@acme.com");
        invited.VexxenUserId = null;
        invited.TokenInvitacion = "invite-token";
        invited.TokenInvitacionExpiracion = DateTime.UtcNow.AddDays(3);
        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(invited);
            await db.SaveChangesAsync();
        }
        var token = Vexxen.AddUser(FakeVexxenClient.User("vx-bea", "Bea@Acme.com", "Bea", "Ruiz"));

        var response = await SignIn(token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<VexxenSessionResponse>())!.Status.Should().Be("signed_in");
        await using var verify = Factory.CreateDbContext();
        var linked = await verify.Users.SingleAsync(u => u.Id == invited.Id);
        linked.VexxenUserId.Should().Be("vx-bea");
        linked.TokenInvitacion.Should().BeNull();
    }

    [Fact]
    public async Task InvitedPerson_WithAnExpiredInvitation_IsNotLinked()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var invited = TestDataFactory.CreateUser(company, email: "bea@acme.com");
        invited.VexxenUserId = null;
        invited.TokenInvitacion = "invite-token";
        invited.TokenInvitacionExpiracion = DateTime.UtcNow.AddDays(-1);
        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(invited);
            await db.SaveChangesAsync();
        }

        var response = await SignIn(Vexxen.AddUser(FakeVexxenClient.User("vx-bea", "bea@acme.com")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("INVITATION_EXPIRED");
        await using var verify = Factory.CreateDbContext();
        (await verify.Users.SingleAsync(u => u.Id == invited.Id)).VexxenUserId.Should().BeNull();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("starter", true)]
    public async Task PersonWithoutCompany_GoesToOnboarding_KnowingIfTheyHaveAPlan(string? planId, bool hasPlan)
    {
        var token = Vexxen.AddUser(FakeVexxenClient.User("vx-new", "new@acme.com", "Nuevo", "Dueño"), planId, "trialing");

        var response = await SignIn(token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<NeedsOnboardingResponse>())!;
        body.Status.Should().Be("needs_onboarding");
        body.Email.Should().Be("new@acme.com");
        body.HasPlan.Should().Be(hasPlan);
        body.CatalogUrl.Should().Be("http://vexxen.test/dashboard/catalog?product=evaluation-flow");
    }

    [Fact]
    public async Task DisabledUser_CantSignIn()
    {
        await SeedMemberAsync(activo: false);

        var response = await SignIn(Vexxen.AddUser(FakeVexxenClient.User("vx-ana", "ana@acme.com")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("USER_INACTIVE");
    }

    [Fact]
    public async Task SuspendedAccount_CantSignIn()
    {
        await SeedMemberAsync();
        // Vexxen's userinfo still accepts the access token for a few minutes after the suspension.
        var token = Vexxen.AddUser(FakeVexxenClient.User("vx-ana", "ana@acme.com"));
        Vexxen.SuspendedUserIds.Add("vx-ana");

        var response = await SignIn(token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("ACCOUNT_SUSPENDED");
    }

    [Fact]
    public async Task SuspendedPersonWithoutCompany_DoesntGoToOnboarding()
    {
        var token = Vexxen.AddUser(FakeVexxenClient.User("vx-new", "new@acme.com"), "starter");
        Vexxen.SuspendedUserIds.Add("vx-new");

        var response = await SignIn(token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ACCOUNT_SUSPENDED");
    }

    [Fact]
    public async Task SignIn_WhenTheSuspensionCheckCantReachVexxen_LetsTheMemberIn()
    {
        await SeedMemberAsync();
        var token = Vexxen.AddUser(FakeVexxenClient.User("vx-ana", "ana@acme.com"));
        Vexxen.AccessDown = true;

        var response = await SignIn(token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<VexxenSessionResponse>())!.Status.Should().Be("signed_in");
    }

    [Fact]
    public async Task CompanyWithoutPlan_CanStillSignIn_ReadOnly()
    {
        await SeedMemberAsync(planStatus: PlanStatuses.None, billing: null);

        var response = await SignIn(Vexxen.AddUser(FakeVexxenClient.User("vx-ana", "ana@acme.com")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<VexxenSessionResponse>())!.PlanStatus.Should().Be(PlanStatuses.None);
    }

    [Fact]
    public async Task InvalidVexxenToken_ReturnsUnauthorized()
    {
        var response = await SignIn("not-a-vexxen-token");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("INVALID_VEXXEN_SESSION");
    }

    [Fact]
    public async Task VexxenDown_ReturnsServiceUnavailable()
    {
        var token = Vexxen.AddUser(FakeVexxenClient.User("vx-ana", "ana@acme.com"));
        Vexxen.Down = true;

        var response = await SignIn(token);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("VEXXEN_UNAVAILABLE");
    }

    [Fact]
    public async Task WithoutApiKey_ReturnsUnauthorized()
    {
        var response = await CreateClient(includeApiKey: false).PostAsJsonAsync("/Auth/vexxen", new VexxenSignInRecord("x"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

/// <summary>POST /Auth/refresh and /Auth/logout: the session lives as long as the Vexxen one.</summary>
public class VexxenSessionEndpointTests : IntegrationTestBase
{
    private FakeVexxenClient Vexxen => Factory.FakeVexxen;

    private async Task<Company> SeedMemberAsync()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1", billingVexxenUserId: "vx-owner");
        // Read from Vexxen long ago: the next refresh reads it again.
        company.PlanSyncedAt = DateTime.UtcNow.AddHours(-1);
        await using var db = Factory.CreateDbContext();
        db.Companies.Add(company);
        db.Users.Add(TestDataFactory.CreateUser(company, email: "ana@acme.com", vexxenUserId: "vx-ana"));
        await db.SaveChangesAsync();
        Vexxen.AddUser(FakeVexxenClient.User("vx-ana", "ana@acme.com"));
        return company;
    }

    private Task<HttpResponseMessage> Refresh(string refreshToken) =>
        Client.PostAsJsonAsync("/Auth/refresh", new RefreshSessionRecord(refreshToken));

    [Fact]
    public async Task Refresh_RotatesTheVexxenToken_AndRereadsThePlan()
    {
        var company = await SeedMemberAsync();
        Vexxen.SetPlan("vx-owner", "starter", "past_due");
        var refreshToken = Vexxen.AddRefreshToken("vx-ana");

        var response = await Refresh(refreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = (await response.Content.ReadFromJsonAsync<VexxenSessionResponse>())!;
        session.Jwt.Should().NotBeNullOrEmpty();
        session.RefreshToken.Should().NotBeNullOrEmpty().And.NotBe(refreshToken);
        session.PlanStatus.Should().Be(PlanStatuses.PastDue);

        await using var db = Factory.CreateDbContext();
        (await db.Companies.SingleAsync(c => c.Id == company.Id)).PlanId.Should().Be(PlanCatalog.Starter);

        // The old refresh token was used up.
        (await Refresh(refreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ChecksThatTheVexxenAccountIsntSuspended()
    {
        await SeedMemberAsync();
        var refreshToken = Vexxen.AddRefreshToken("vx-ana");

        (await Refresh(refreshToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        Vexxen.AccountStatusCalls.Should().Be(1);
        Vexxen.LoggedOut.Should().BeEmpty();
    }

    [Fact]
    public async Task Refresh_AfterTheVexxenAccountIsSuspended_EndsTheSession_AndRevokesTheNewRefreshToken()
    {
        await SeedMemberAsync();
        var refreshToken = Vexxen.AddRefreshToken("vx-ana");
        Vexxen.SuspendedUserIds.Add("vx-ana");

        var response = await Refresh(refreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("ACCOUNT_SUSPENDED");
        // The token the frontend sent was spent by the refresh; the pair Vexxen issued with it is revoked straight away.
        var revoked = Vexxen.LoggedOut.Should().ContainSingle().Which;
        revoked.Should().NotBe(refreshToken);
        Vexxen.RefreshTokens.Should().BeEmpty("no refresh token of the suspended account is left alive");
    }

    [Fact]
    public async Task Refresh_WhenVexxenItselfRefusesASuspendedAccount_Returns401Suspended()
    {
        await SeedMemberAsync();
        var refreshToken = Vexxen.AddRefreshToken("vx-ana");
        Vexxen.SuspendedUserIds.Add("vx-ana");
        Vexxen.RefreshChecksSuspension = true;

        var response = await Refresh(refreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ACCOUNT_SUSPENDED");
    }

    [Fact]
    public async Task Refresh_WhenTheSuspensionCheckCantReachVexxen_StillRenewsTheSession()
    {
        await SeedMemberAsync();
        var refreshToken = Vexxen.AddRefreshToken("vx-ana");
        Vexxen.AccessDown = true;

        var response = await Refresh(refreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = (await response.Content.ReadFromJsonAsync<VexxenSessionResponse>())!;
        session.Jwt.Should().NotBeNullOrEmpty();
        session.RefreshToken.Should().NotBeNullOrEmpty();
        Vexxen.LoggedOut.Should().BeEmpty();
    }

    [Fact]
    public async Task Refresh_AfterTheVexxenAccountIsDeleted_EndsTheSession()
    {
        await SeedMemberAsync();
        var refreshToken = Vexxen.AddRefreshToken("vx-ana");
        Vexxen.DeletedUserIds.Add("vx-ana");

        var response = await Refresh(refreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("INVALID_VEXXEN_SESSION");
    }

    [Fact]
    public async Task Refresh_WhenNoLongerInAnyCompany_ReturnsNotAMember()
    {
        await SeedMemberAsync();
        Vexxen.AddUser(FakeVexxenClient.User("vx-gone", "gone@acme.com"));
        var refreshToken = Vexxen.AddRefreshToken("vx-gone");

        var response = await Refresh(refreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("NOT_A_MEMBER");
    }

    [Fact]
    public async Task Refresh_WhenVexxenIsDown_ReturnsServiceUnavailable()
    {
        await SeedMemberAsync();
        var refreshToken = Vexxen.AddRefreshToken("vx-ana");
        Vexxen.Down = true;

        (await Refresh(refreshToken)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Logout_RevokesTheVexxenRefreshToken()
    {
        await SeedMemberAsync();
        var refreshToken = Vexxen.AddRefreshToken("vx-ana");

        var response = await Client.PostAsJsonAsync("/Auth/logout", new LogoutRecord(refreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Vexxen.LoggedOut.Should().ContainSingle().Which.Should().Be(refreshToken);
        (await Refresh(refreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WhenVexxenIsDown_StillAnswersNoContent()
    {
        Vexxen.Down = true;

        var response = await Client.PostAsJsonAsync("/Auth/logout", new LogoutRecord("whatever"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

/// <summary>The old email/password flows answer 410 USE_VEXXEN.</summary>
public class RetiredAuthEndpointTests : IntegrationTestBase
{
    [Theory]
    [InlineData("POST", "/Auth/login")]
    [InlineData("POST", "/auth/verify-email")]
    [InlineData("POST", "/auth/resend-verification")]
    [InlineData("POST", "/auth/verify-2fa")]
    [InlineData("POST", "/auth/resend-2fa")]
    [InlineData("POST", "/auth/forgot-password")]
    [InlineData("POST", "/auth/reset-password")]
    [InlineData("PUT", "/settings/2fa")]
    [InlineData("PUT", "/Profile/change-password")]
    [InlineData("PUT", "/Profile/update")]
    [InlineData("POST", "/team/accept-invite")]
    public async Task OldEndpoints_AnswerGone(string method, string route)
    {
        var response = await Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route)
        {
            Content = JsonContent.Create(new { }),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("USE_VEXXEN");
    }
}
