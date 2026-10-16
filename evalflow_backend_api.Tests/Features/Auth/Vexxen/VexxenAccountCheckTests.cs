using evalflow_backend_api.Features.Auth.Vexxen;
using evalflow_backend_api.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace evalflow_backend_api.Tests.Features.Auth.Vexxen;

/// <summary>
/// <see cref="VexxenSessionService.CheckAccountAsync"/>: suspended or deleted Vexxen accounts get no EvalFlow session,
/// and a Vexxen that doesn't answer never locks anyone out.
/// </summary>
public class VexxenAccountCheckTests
{
    private readonly FakeVexxenClient _vexxen = new();

    // Only the Vexxen client is involved in the check.
    private VexxenSessionService Sut => new(null!, null!, null!, _vexxen, NullLogger<VexxenSessionService>.Instance);

    private static string? CodeOf(IResult? result) =>
        (result as IValueHttpResult)?.Value?.GetType().GetProperty("Code")?.GetValue((result as IValueHttpResult)!.Value) as string;

    [Fact]
    public async Task ActiveAccount_GoesAhead()
    {
        (await Sut.CheckAccountAsync("vx-ana", StatusCodes.Status401Unauthorized, CancellationToken.None)).Should().BeNull();
    }

    [Theory]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status403Forbidden)]
    public async Task SuspendedAccount_AccountSuspended_WithTheStatusAsked(int status)
    {
        _vexxen.SuspendedUserIds.Add("vx-ana");

        var result = await Sut.CheckAccountAsync("vx-ana", status, CancellationToken.None);

        result.Should().HaveStatusCode(status);
        CodeOf(result).Should().Be("ACCOUNT_SUSPENDED");
    }

    [Fact]
    public async Task DeletedAccount_InvalidSession401()
    {
        _vexxen.DeletedUserIds.Add("vx-ana");

        var result = await Sut.CheckAccountAsync("vx-ana", StatusCodes.Status403Forbidden, CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status401Unauthorized);
        CodeOf(result).Should().Be("INVALID_VEXXEN_SESSION");
    }

    [Fact]
    public async Task VexxenUnreachable_GoesAhead()
    {
        _vexxen.SuspendedUserIds.Add("vx-ana");
        _vexxen.AccessDown = true;

        (await Sut.CheckAccountAsync("vx-ana", StatusCodes.Status401Unauthorized, CancellationToken.None)).Should().BeNull();
    }
}
