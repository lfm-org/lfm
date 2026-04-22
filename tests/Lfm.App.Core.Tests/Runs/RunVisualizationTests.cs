// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.App.Runs;
using Lfm.Contracts.Runs;
using Xunit;

namespace Lfm.App.Core.Tests.Runs;

public class RunVisualizationTests
{
    [Theory]
    [InlineData(5, RunKind.Dungeon)]
    [InlineData(10, RunKind.Raid)]
    [InlineData(20, RunKind.Raid)]
    [InlineData(25, RunKind.Raid)]
    [InlineData(30, RunKind.Raid)]
    [InlineData(0, RunKind.Unknown)]
    [InlineData(-1, RunKind.Unknown)]
    public void GetKind_classifies_size_into_run_kind(int size, RunKind expected)
    {
        Assert.Equal(expected, RunVisualization.GetKind(size));
    }

    [Theory]
    [InlineData(5, 1, 1, 3)]
    [InlineData(10, 2, 2, 6)]
    [InlineData(20, 2, 4, 14)]
    [InlineData(25, 2, 5, 18)]
    [InlineData(30, 2, 6, 22)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(40, 0, 0, 0)]
    public void GetRoleTargets_returns_standard_comp(int size, int tanks, int healers, int dps)
    {
        Assert.Equal((tanks, healers, dps), RunVisualization.GetRoleTargets(size));
    }

    [Fact]
    public void CountRoles_counts_attending_per_role_and_fills_targets()
    {
        var chars = new List<RunCharacterDto>
        {
            MakeChar("TANK", "IN"),
            MakeChar("TANK", "OUT"),
            MakeChar("HEALER", "IN"),
            MakeChar("HEALER", "LATE"),
            MakeChar("HEALER", "BENCH"),
            MakeChar("DPS", "IN"),
            MakeChar("DPS", "AWAY"),
            MakeChar(null, "IN"),
        };

        var counts = RunVisualization.CountRoles(chars, size: 20);

        Assert.Equal(1, counts.Tank.Attending);
        Assert.Equal(2, counts.Tank.Target);
        Assert.Equal(3, counts.Healer.Attending);
        Assert.Equal(4, counts.Healer.Target);
        Assert.Equal(2, counts.Dps.Attending);
        Assert.Equal(14, counts.Dps.Target);
    }

    [Fact]
    public void CountRoles_marks_shortage_when_attending_below_target()
    {
        var chars = new List<RunCharacterDto> { MakeChar("HEALER", "IN") };
        var counts = RunVisualization.CountRoles(chars, size: 10);
        Assert.True(counts.Tank.IsShortage);
        Assert.True(counts.Healer.IsShortage);
        Assert.True(counts.Dps.IsShortage);
    }

    [Fact]
    public void CountRoles_no_shortage_when_size_unknown()
    {
        var chars = new List<RunCharacterDto> { MakeChar("TANK", "IN") };
        var counts = RunVisualization.CountRoles(chars, size: 0);
        Assert.False(counts.Tank.IsShortage);
        Assert.False(counts.Healer.IsShortage);
        Assert.False(counts.Dps.IsShortage);
    }

    [Theory]
    [InlineData("IN", true)]
    [InlineData("LATE", true)]
    [InlineData("BENCH", true)]
    [InlineData("OUT", false)]
    [InlineData("AWAY", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAttending_covers_all_states(string? attendance, bool expected)
    {
        Assert.Equal(expected, RunVisualization.IsAttending(attendance));
    }

    [Fact]
    public void IsCurrentUserSignedUp_true_when_any_character_is_current_user()
    {
        var chars = new List<RunCharacterDto>
        {
            MakeChar("TANK", "IN", isCurrentUser: false),
            MakeChar("DPS", "IN", isCurrentUser: true),
        };
        Assert.True(RunVisualization.IsCurrentUserSignedUp(chars));
    }

    [Fact]
    public void IsCurrentUserSignedUp_false_when_empty_or_no_match()
    {
        Assert.False(RunVisualization.IsCurrentUserSignedUp(Array.Empty<RunCharacterDto>()));
        Assert.False(RunVisualization.IsCurrentUserSignedUp(new[] { MakeChar("TANK", "IN") }));
    }

    [Fact]
    public void GetHorizon_classifies_runs_against_fixed_now()
    {
        var now = new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(TimeHorizon.Past, RunVisualization.GetHorizon("2026-04-19T10:00:00+00:00", now));
        Assert.Equal(TimeHorizon.ThisWeek, RunVisualization.GetHorizon("2026-04-23T19:00:00+00:00", now));
        Assert.Equal(TimeHorizon.NextWeek, RunVisualization.GetHorizon("2026-04-29T19:00:00+00:00", now));
        Assert.Equal(TimeHorizon.Later, RunVisualization.GetHorizon("2026-05-15T19:00:00+00:00", now));
    }

    [Fact]
    public void GetHorizon_returns_unknown_on_unparseable_input()
    {
        var now = new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(TimeHorizon.Unknown, RunVisualization.GetHorizon("", now));
        Assert.Equal(TimeHorizon.Unknown, RunVisualization.GetHorizon(null, now));
        Assert.Equal(TimeHorizon.Unknown, RunVisualization.GetHorizon("not a date", now));
    }

    [Theory]
    [InlineData("MYTHIC", "mythic")]
    [InlineData("HEROIC", "heroic")]
    [InlineData("NORMAL", "normal")]
    [InlineData("LFR", "lfr")]
    [InlineData("weird", "unknown")]
    [InlineData("", "unknown")]
    [InlineData(null, "unknown")]
    public void GetDifficultyClass_maps_difficulty_token(string? difficulty, string expected)
    {
        Assert.Equal(expected, RunVisualization.GetDifficultyClass(difficulty));
    }

    [Theory]
    [InlineData(RunKind.Dungeon, "dungeon")]
    [InlineData(RunKind.Raid, "raid")]
    [InlineData(RunKind.Unknown, "unknown")]
    public void GetKindClass_maps_kind_enum(RunKind kind, string expected)
    {
        Assert.Equal(expected, RunVisualization.GetKindClass(kind));
    }

    private static RunCharacterDto MakeChar(string? role, string attendance, bool isCurrentUser = false) =>
        new(
            CharacterName: "Char",
            CharacterRealm: "Realm",
            CharacterClassId: 1,
            CharacterClassName: "Warrior",
            DesiredAttendance: attendance,
            ReviewedAttendance: attendance,
            SpecName: null,
            Role: role,
            IsCurrentUser: isCurrentUser);
}
