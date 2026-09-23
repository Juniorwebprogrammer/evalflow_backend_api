using FluentAssertions;
using FluentAssertions.Primitives;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Tests.Common;

/// <summary>
/// Handlers return anonymous-typed payloads (e.g. Results.BadRequest(new { Message = "..." })),
/// so `result.Should().BeOfType&lt;BadRequest&lt;object&gt;&gt;()` never matches — the real generic
/// argument is the anonymous type, not object. Assert on the status code instead, via the
/// IStatusCodeHttpResult interface every Results.* helper implements.
/// </summary>
public static class ResultAssertionExtensions
{
    public static AndConstraint<ObjectAssertions> HaveStatusCode(this ObjectAssertions assertions, int statusCode, string because = "", params object[] becauseArgs)
    {
        var subject = assertions.Subject.Should().BeAssignableTo<IStatusCodeHttpResult>(because, becauseArgs).Subject;
        subject.StatusCode.Should().Be(statusCode, because, becauseArgs);
        return new AndConstraint<ObjectAssertions>(assertions);
    }
}
