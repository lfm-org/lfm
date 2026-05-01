// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Xunit;

namespace Lfm.Api.Tests;

public class GuildRosterMatcherTests
{
    private static StoredGuildRoster Roster(params StoredGuildRosterMember[] members) =>
        new(members);

    private static StoredSelectedCharacter Character(
        string name = "Aelrin",
        string realm = "silvermoon") =>
        new(
            Id: $"eu-{realm}-{name.ToLowerInvariant()}",
            Region: "eu",
            Realm: realm,
            Name: name);

    [Fact]
    public void Match_returns_rank_for_case_insensitive_realm_and_name()
    {
        var roster = Roster(new StoredGuildRosterMember(
            new StoredGuildRosterMemberCharacter(
                Name: "Aelrin",
                Realm: new StoredGuildRosterRealm("Silvermoon")),
            Rank: 4));

        var match = GuildRosterMatcher.Match(roster, Character(name: "aelrin", realm: "silvermoon"));

        Assert.NotNull(match);
        Assert.Equal(4, match.Rank);
        Assert.Equal("silvermoon:aelrin", match.CharacterKey);
    }

    [Fact]
    public void Match_returns_null_when_character_is_not_in_roster()
    {
        var roster = Roster(new StoredGuildRosterMember(
            new StoredGuildRosterMemberCharacter(
                Name: "Other",
                Realm: new StoredGuildRosterRealm("silvermoon")),
            Rank: 7));

        var match = GuildRosterMatcher.Match(roster, Character());

        Assert.Null(match);
    }

    [Fact]
    public void BestRank_returns_lowest_rank_across_matching_characters()
    {
        var roster = Roster(
            new StoredGuildRosterMember(
                new StoredGuildRosterMemberCharacter("Alt", new StoredGuildRosterRealm("silvermoon")),
                Rank: 6),
            new StoredGuildRosterMember(
                new StoredGuildRosterMemberCharacter("Main", new StoredGuildRosterRealm("silvermoon")),
                Rank: 2));

        var rank = GuildRosterMatcher.BestRank(roster, [
            Character(name: "Alt"),
            Character(name: "Main")
        ]);

        Assert.Equal(2, rank);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("not-a-date", false)]
    public void IsFresh_returns_false_for_missing_or_invalid_timestamps(string? timestamp, bool expected)
    {
        Assert.Equal(expected, GuildRosterMatcher.IsFresh(timestamp));
    }

    [Theory]
    [InlineData(59, true)]
    [InlineData(60, false)]
    [InlineData(61, false)]
    public void IsFresh_treats_one_hour_as_stale_boundary(int ageMinutes, bool expected)
    {
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-ageMinutes).ToString("o");

        Assert.Equal(expected, GuildRosterMatcher.IsFresh(timestamp));
    }
}
