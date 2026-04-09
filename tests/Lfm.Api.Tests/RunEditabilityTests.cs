using FluentAssertions;
using Lfm.Api.Helpers;
using Xunit;

namespace Lfm.Api.Tests;

public class RunEditabilityTests
{
    [Fact]
    public void Returns_false_when_both_times_are_in_the_future()
    {
        var future = DateTimeOffset.UtcNow.AddHours(2).ToString("o");
        var farFuture = DateTimeOffset.UtcNow.AddHours(4).ToString("o");

        RunEditability.IsEditingClosed(future, farFuture, DateTimeOffset.UtcNow)
            .Should().BeFalse();
    }

    [Fact]
    public void Returns_true_when_signup_close_time_has_passed()
    {
        var past = DateTimeOffset.UtcNow.AddHours(-1).ToString("o");
        var future = DateTimeOffset.UtcNow.AddHours(4).ToString("o");

        RunEditability.IsEditingClosed(past, future, DateTimeOffset.UtcNow)
            .Should().BeTrue();
    }

    [Fact]
    public void Returns_true_when_start_time_has_passed()
    {
        var past = DateTimeOffset.UtcNow.AddHours(-1).ToString("o");

        RunEditability.IsEditingClosed("", past, DateTimeOffset.UtcNow)
            .Should().BeTrue();
    }

    [Fact]
    public void Returns_false_when_both_times_are_null()
    {
        RunEditability.IsEditingClosed(null, null, DateTimeOffset.UtcNow)
            .Should().BeFalse();
    }

    [Fact]
    public void Returns_false_when_both_times_are_empty()
    {
        RunEditability.IsEditingClosed("", "", DateTimeOffset.UtcNow)
            .Should().BeFalse();
    }

    [Fact]
    public void Returns_true_when_signup_close_time_equals_now()
    {
        var now = DateTimeOffset.UtcNow;
        var exact = now.ToString("o");

        RunEditability.IsEditingClosed(exact, "", now)
            .Should().BeTrue();
    }

    [Fact]
    public void Returns_false_when_signup_close_time_is_unparseable()
    {
        RunEditability.IsEditingClosed("not-a-date", "", DateTimeOffset.UtcNow)
            .Should().BeFalse();
    }
}
