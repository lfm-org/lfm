// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.App.Runs;
using Xunit;

namespace Lfm.App.Core.Tests.Runs;

public class RunTimeDefaultsTests
{
    [Theory]
    [InlineData("2026-04-20T10:00:00", "2026-04-23T20:00:00")] // Monday → this Thursday
    [InlineData("2026-04-21T10:00:00", "2026-04-23T20:00:00")] // Tuesday → this Thursday
    [InlineData("2026-04-23T10:00:00", "2026-04-23T20:00:00")] // Thursday morning → today 20:00
    [InlineData("2026-04-23T20:00:00", "2026-04-30T20:00:00")] // Thursday at exactly 20:00 → next week
    [InlineData("2026-04-23T22:00:00", "2026-04-30T20:00:00")] // Thursday evening → next week
    [InlineData("2026-04-25T10:00:00", "2026-04-30T20:00:00")] // Saturday → next Thursday
    [InlineData("2026-04-26T10:00:00", "2026-04-30T20:00:00")] // Sunday → next Thursday
    public void NextThursday20_returns_next_strict_future_Thursday_at_8pm(string now, string expected)
    {
        var nowDt = DateTime.Parse(now);
        var expectedDt = DateTime.Parse(expected);
        Assert.Equal(expectedDt, RunTimeDefaults.NextThursday20(nowDt));
    }

    [Fact]
    public void Result_is_always_strictly_in_the_future()
    {
        var now = DateTime.Parse("2026-04-23T21:00:00"); // Thursday 21:00
        var next = RunTimeDefaults.NextThursday20(now);
        Assert.True(next > now, $"expected next > now but got next={next:O} now={now:O}");
    }
}
