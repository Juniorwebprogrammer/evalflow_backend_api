using System.Net;
using System.Text;
using evalflow_backend_api.Infrastructure.Vexxen;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace evalflow_backend_api.Tests.Infrastructure.Vexxen;

/// <summary>The HTTP client against responses shaped exactly like Vexxen's (vexxen-home /api/auth and /api/access).</summary>
public class VexxenClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string?> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static (VexxenClient Client, StubHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://vexxen.test/") };
        var settings = Options.Create(new VexxenSettings { BaseUrl = "http://vexxen.test", ClientId = "vexxen_f873382b", ApiKey = "secret-key" });
        return (new VexxenClient(http, settings, NullLogger<VexxenClient>.Instance), handler);
    }

    [Fact]
    public async Task UserInfo_SendsTheClientCredentialsAndTheUserToken()
    {
        var (client, handler) = Create(_ => Json(HttpStatusCode.OK,
            """{"id":"u-1","firstName":"Ana","lastName":"Pérez","email":"ana@acme.com","emailVerified":true,"avatarUrl":null,"phone":null,"linkedProviders":[]}"""));

        var user = await client.GetUserInfoAsync("access-token", CancellationToken.None);

        user.Should().Be(new VexxenUser("u-1", "Ana", "Pérez", "ana@acme.com", true));
        var request = handler.Requests.Single();
        request.RequestUri!.ToString().Should().Be("http://vexxen.test/api/auth/userinfo");
        request.Headers.GetValues("X-Client-Id").Should().Equal("vexxen_f873382b");
        request.Headers.GetValues("X-Api-Key").Should().Equal("secret-key");
        request.Headers.Authorization!.ToString().Should().Be("Bearer access-token");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task UserInfo_WithAnInvalidToken_ThrowsUnauthorized(HttpStatusCode status)
    {
        var (client, _) = Create(_ => Json(status, """{"error":"Invalid or expired access token"}"""));

        await FluentActions.Awaiting(() => client.GetUserInfoAsync("x", CancellationToken.None))
            .Should().ThrowAsync<VexxenUnauthorizedException>();
    }

    [Fact]
    public async Task Access_ReadsThePlanAndTheDates()
    {
        var (client, handler) = Create(_ => Json(HttpStatusCode.OK,
            """{"productId":"evaluation-flow","access":true,"status":"trialing","plan":{"id":"professional","name":"Professional","paid":true},"cycle":"monthly","currentPeriodEnd":"2026-10-28T10:00:00.000Z","cancelAtPeriodEnd":false,"trialEndsAt":"2026-10-28T10:00:00.000Z","accessEndsAt":null}"""));

        var access = await client.GetAccessAsync("u 1", CancellationToken.None);

        access.Access.Should().BeTrue();
        access.Status.Should().Be("trialing");
        access.Plan!.Id.Should().Be("professional");
        access.TrialEndsAt.Should().Be(new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc));
        access.TrialEndsAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        access.AccessEndsAt.Should().BeNull();
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/api/access/u%201");
    }

    [Fact]
    public async Task Access_WithoutTheProduct_HasNoPlan()
    {
        var (client, _) = Create(_ => Json(HttpStatusCode.OK,
            """{"productId":"evaluation-flow","access":false,"status":"none","plan":null,"cycle":null,"currentPeriodEnd":null,"cancelAtPeriodEnd":false,"trialEndsAt":null,"accessEndsAt":null,"transferredTo":"u-2"}"""));

        var access = await client.GetAccessAsync("u-1", CancellationToken.None);

        access.Access.Should().BeFalse();
        access.Plan.Should().BeNull();
        access.TransferredTo.Should().Be("u-2");
    }

    [Fact]
    public async Task Access_ForADeletedAccount_ThrowsUserNotFound()
    {
        var (client, _) = Create(_ => Json(HttpStatusCode.NotFound, """{"error":"User not found","code":"USER_NOT_FOUND"}"""));

        await FluentActions.Awaiting(() => client.GetAccessAsync("u-1", CancellationToken.None))
            .Should().ThrowAsync<VexxenUserNotFoundException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, """{"error":"This client isn't linked to a product","code":"CLIENT_WITHOUT_PRODUCT"}""")]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":"Invalid API key"}""")]
    [InlineData(HttpStatusCode.InternalServerError, """{"error":"Internal error"}""")]
    [InlineData(HttpStatusCode.OK, "<html>not json</html>")]
    public async Task Access_ConfigurationOrServerProblems_ThrowUnavailable(HttpStatusCode status, string body)
    {
        var (client, _) = Create(_ => Json(status, body));

        await FluentActions.Awaiting(() => client.GetAccessAsync("u-1", CancellationToken.None))
            .Should().ThrowAsync<VexxenUnavailableException>();
    }

    [Fact]
    public async Task Access_ForASuspendedAccount_ThrowsSuspended()
    {
        var (client, _) = Create(_ => Json(HttpStatusCode.Forbidden, """{"error":"This account is suspended","code":"ACCOUNT_SUSPENDED"}"""));

        await FluentActions.Awaiting(() => client.GetAccessAsync("u-1", CancellationToken.None))
            .Should().ThrowAsync<VexxenAccountSuspendedException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"productId":"evaluation-flow","access":false,"status":"none","plan":null,"cycle":null,"currentPeriodEnd":null,"cancelAtPeriodEnd":false,"trialEndsAt":null,"accessEndsAt":null}""", VexxenAccountStatus.Active)]
    [InlineData(HttpStatusCode.Forbidden, """{"error":"This account is suspended","code":"ACCOUNT_SUSPENDED"}""", VexxenAccountStatus.Suspended)]
    [InlineData(HttpStatusCode.NotFound, """{"error":"User not found","code":"USER_NOT_FOUND"}""", VexxenAccountStatus.Deleted)]
    public async Task AccountStatus_ReadsTheAccessEndpoint(HttpStatusCode status, string body, VexxenAccountStatus expected)
    {
        var (client, handler) = Create(_ => Json(status, body));

        (await client.GetAccountStatusAsync("u 1", CancellationToken.None)).Should().Be(expected);

        var request = handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.Should().Be("/api/access/u%201");
        request.Headers.GetValues("X-Client-Id").Should().Equal("vexxen_f873382b");
        request.Headers.GetValues("X-Api-Key").Should().Equal("secret-key");
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, """{"error":"This client isn't linked to a product","code":"CLIENT_WITHOUT_PRODUCT"}""")]
    [InlineData(HttpStatusCode.NotFound, """{"error":"Product not found","code":"PRODUCT_NOT_FOUND"}""")]
    [InlineData(HttpStatusCode.InternalServerError, """{"error":"Internal error"}""")]
    [InlineData(HttpStatusCode.BadGateway, "<html>bad gateway</html>")]
    public async Task AccountStatus_ConfigurationOrServerProblems_ThrowUnavailable(HttpStatusCode status, string body)
    {
        var (client, _) = Create(_ => Json(status, body));

        await FluentActions.Awaiting(() => client.GetAccountStatusAsync("u-1", CancellationToken.None))
            .Should().ThrowAsync<VexxenUnavailableException>();
    }

    [Fact]
    public async Task AccountStatus_NetworkFailure_ThrowsUnavailable()
    {
        var (client, _) = Create(_ => throw new TaskCanceledException("timeout"));

        await FluentActions.Awaiting(() => client.GetAccountStatusAsync("u-1", CancellationToken.None))
            .Should().ThrowAsync<VexxenUnavailableException>();
    }

    [Fact]
    public async Task NetworkFailure_ThrowsUnavailable()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("connection refused"));

        await FluentActions.Awaiting(() => client.GetUserInfoAsync("x", CancellationToken.None))
            .Should().ThrowAsync<VexxenUnavailableException>();
    }

    [Fact]
    public async Task Refresh_PostsTheTokenAndReadsTheNewPair()
    {
        var (client, handler) = Create(_ => Json(HttpStatusCode.OK, """{"accessToken":"a2","refreshToken":"r2"}"""));

        var tokens = await client.RefreshAsync("r1", CancellationToken.None);

        tokens.Should().Be(new VexxenTokens("a2", "r2"));
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/api/auth/refreshToken");
        handler.Bodies.Single().Should().Be("""{"refreshToken":"r1"}""");
    }

    [Fact]
    public async Task Refresh_WithAnExpiredToken_ThrowsUnauthorized()
    {
        var (client, _) = Create(_ => Json(HttpStatusCode.Unauthorized, """{"error":"Invalid refresh token","code":"INVALID_REFRESH_TOKEN"}"""));

        await FluentActions.Awaiting(() => client.RefreshAsync("r1", CancellationToken.None))
            .Should().ThrowAsync<VexxenUnauthorizedException>();
    }

    [Fact]
    public async Task Refresh_OfASuspendedAccount_ThrowsSuspended()
    {
        var (client, _) = Create(_ => Json(HttpStatusCode.Forbidden,
            """{"error":"This account has been suspended. Contact support if you think this is a mistake.","code":"ACCOUNT_SUSPENDED"}"""));

        await FluentActions.Awaiting(() => client.RefreshAsync("r1", CancellationToken.None))
            .Should().ThrowAsync<VexxenAccountSuspendedException>();
    }

    [Fact]
    public async Task Refresh_OfADeletedAccount_ThrowsUnauthorized()
    {
        var (client, _) = Create(_ => Json(HttpStatusCode.NotFound, """{"error":"This account no longer exists","code":"ACCOUNT_NOT_FOUND"}"""));

        await FluentActions.Awaiting(() => client.RefreshAsync("r1", CancellationToken.None))
            .Should().ThrowAsync<VexxenUnauthorizedException>();
    }

    [Fact]
    public async Task Logout_PostsTheToken_AndDoesntThrowOnErrors()
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        await client.LogoutAsync("r1", CancellationToken.None);

        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/api/auth/logout");
    }
}
