using FluentAssertions;
using Lfm.Api.Functions;
using Lfm.Contracts.Health;
using Xunit;

namespace Lfm.Api.Tests;

public class HealthFunctionTests
{
    [Fact]
    public void Health_returns_ok_status_and_timestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var result = HealthFunction.Build();
        var after = DateTimeOffset.UtcNow;

        result.Should().BeOfType<HealthResponse>();
        result.Status.Should().Be("ok");
        result.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
