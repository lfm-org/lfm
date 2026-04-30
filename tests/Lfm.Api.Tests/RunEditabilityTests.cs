// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Runs;
using Xunit;

namespace Lfm.Api.Tests;

public class RunEditabilityTests
{
    [Fact]
    public void Returns_false_when_both_times_are_in_the_future()
    {
        var future = DateTimeOffset.UtcNow.AddHours(2).ToString("o");
        var farFuture = DateTimeOffset.UtcNow.AddHours(4).ToString("o");

        Assert.False(RunEditability.IsEditingClosed(future, farFuture, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Returns_true_when_signup_close_time_has_passed()
    {
        var past = DateTimeOffset.UtcNow.AddHours(-1).ToString("o");
        var future = DateTimeOffset.UtcNow.AddHours(4).ToString("o");

        Assert.True(RunEditability.IsEditingClosed(past, future, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Returns_true_when_start_time_has_passed()
    {
        var past = DateTimeOffset.UtcNow.AddHours(-1).ToString("o");

        Assert.True(RunEditability.IsEditingClosed("", past, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Returns_false_when_both_times_are_null()
    {
        Assert.False(RunEditability.IsEditingClosed(null, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Returns_false_when_both_times_are_empty()
    {
        Assert.False(RunEditability.IsEditingClosed("", "", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Returns_true_when_signup_close_time_equals_now()
    {
        var now = DateTimeOffset.UtcNow;
        var exact = now.ToString("o");

        Assert.True(RunEditability.IsEditingClosed(exact, "", now));
    }

    [Fact]
    public void Returns_true_when_start_time_equals_now()
    {
        var now = DateTimeOffset.UtcNow;
        var exact = now.ToString("o");

        Assert.True(RunEditability.IsEditingClosed("", exact, now));
    }

    [Fact]
    public void Returns_false_when_signup_close_time_is_unparseable()
    {
        Assert.False(RunEditability.IsEditingClosed("not-a-date", "", DateTimeOffset.UtcNow));
    }
}
