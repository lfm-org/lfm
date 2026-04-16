// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Moq;
using Xunit;

namespace Lfm.Api.Tests;

public class GuildPermissionsTests
{
    private static GuildDocument MakeGuildDoc(
        string id = "42",
        IReadOnlyList<BlizzardGuildRosterMember>? members = null,
        DateTimeOffset? rosterFetchedAt = null,
        IReadOnlyList<GuildRankPermission>? rankPermissions = null)
    {
        var resolvedFetched = rosterFetchedAt ?? DateTimeOffset.UtcNow.AddMinutes(-5);
        return new GuildDocument(
            Id: id,
            GuildId: 42,
            RealmSlug: "silvermoon",
            BlizzardRosterFetchedAt: resolvedFetched.ToString("o"),
            RankPermissions: rankPermissions,
            BlizzardRosterRaw: new BlizzardGuildRosterRaw(
                Members: members ?? [
                    new BlizzardGuildRosterMember(
                        Character: new BlizzardGuildRosterMemberCharacter(
                            Name: "Gm",
                            Realm: new BlizzardGuildRosterRealm(Slug: "silvermoon"),
                            Id: 1),
                        Rank: 0)
                ]));
    }

    /// <summary>
    /// Makes a raider whose selected character belongs to guild 42 (matching the
    /// default MakeGuildDoc). The GuildId on the character is what GuildResolver
    /// uses to derive the guild — tests that verify "no guild" behaviour should
    /// pass a raider with GuildId: null on the selected character.
    /// </summary>
    private static RaiderDocument MakeRaiderDoc(
        string battleNetId = "bnet-1",
        string characterName = "Gm",
        string realm = "silvermoon",
        int? guildId = 42) =>
        new(
            Id: battleNetId,
            BattleNetId: battleNetId,
            SelectedCharacterId: "char-1",
            Locale: null,
            Characters: [
                new StoredSelectedCharacter(
                    Id: "char-1",
                    Region: "eu",
                    Realm: realm,
                    Name: characterName,
                    GuildId: guildId,
                    GuildName: guildId.HasValue ? "Test Guild" : null)
            ]);

    private static GuildPermissions MakeSut(GuildDocument? guild)
    {
        var guildRepo = new Mock<IGuildRepository>();
        guildRepo.Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        return new GuildPermissions(guildRepo.Object);
    }

    // ─── IsAdminAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task IsAdminAsync_returns_false_when_raider_has_no_guild()
    {
        var raider = MakeRaiderDoc(guildId: null);
        var sut = MakeSut(MakeGuildDoc());

        var result = await sut.IsAdminAsync(raider, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_when_guild_not_found()
    {
        var sut = MakeSut(guild: null);

        var result = await sut.IsAdminAsync(MakeRaiderDoc(), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsAdminAsync_returns_true_when_character_matches_rank_zero_member()
    {
        var sut = MakeSut(MakeGuildDoc());

        var result = await sut.IsAdminAsync(MakeRaiderDoc(characterName: "Gm"), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_when_character_matches_non_zero_rank()
    {
        var guild = MakeGuildDoc(members: [
            new BlizzardGuildRosterMember(
                Character: new BlizzardGuildRosterMemberCharacter(
                    Name: "Gm",
                    Realm: new BlizzardGuildRosterRealm(Slug: "silvermoon"),
                    Id: 1),
                Rank: 3)
        ]);
        var sut = MakeSut(guild);

        var result = await sut.IsAdminAsync(MakeRaiderDoc(characterName: "Gm"), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_when_no_character_matches()
    {
        var sut = MakeSut(MakeGuildDoc());

        var result = await sut.IsAdminAsync(MakeRaiderDoc(characterName: "Nomatch"), CancellationToken.None);

        Assert.False(result);
    }

    // ─── CanCreateGuildRunsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CanCreateGuildRunsAsync_returns_false_when_roster_is_stale()
    {
        var guild = MakeGuildDoc(rosterFetchedAt: DateTimeOffset.UtcNow.AddHours(-2));
        var sut = MakeSut(guild);

        var result = await sut.CanCreateGuildRunsAsync(MakeRaiderDoc(), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CanCreateGuildRunsAsync_returns_false_when_roster_fetched_at_is_null()
    {
        var guild = MakeGuildDoc() with { BlizzardRosterFetchedAt = null };
        var sut = MakeSut(guild);

        var result = await sut.CanCreateGuildRunsAsync(MakeRaiderDoc(), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CanCreateGuildRunsAsync_returns_true_for_rank_zero_by_default()
    {
        var sut = MakeSut(MakeGuildDoc());

        var result = await sut.CanCreateGuildRunsAsync(MakeRaiderDoc(characterName: "Gm"), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task CanCreateGuildRunsAsync_returns_false_for_non_zero_rank_by_default()
    {
        var guild = MakeGuildDoc(members: [
            new BlizzardGuildRosterMember(
                Character: new BlizzardGuildRosterMemberCharacter(
                    Name: "Officer",
                    Realm: new BlizzardGuildRosterRealm(Slug: "silvermoon"),
                    Id: 1),
                Rank: 2)
        ]);
        var sut = MakeSut(guild);

        var result = await sut.CanCreateGuildRunsAsync(MakeRaiderDoc(characterName: "Officer"), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CanCreateGuildRunsAsync_honours_explicit_rank_permission_entry()
    {
        var guild = MakeGuildDoc(
            members: [
                new BlizzardGuildRosterMember(
                    Character: new BlizzardGuildRosterMemberCharacter(
                        Name: "Officer",
                        Realm: new BlizzardGuildRosterRealm(Slug: "silvermoon"),
                        Id: 1),
                    Rank: 2)
            ],
            rankPermissions: [
                new GuildRankPermission(Rank: 2, CanCreateGuildRuns: true, CanSignupGuildRuns: true, CanDeleteGuildRuns: false)
            ]);
        var sut = MakeSut(guild);

        var result = await sut.CanCreateGuildRunsAsync(MakeRaiderDoc(characterName: "Officer"), CancellationToken.None);

        Assert.True(result);
    }

    // ─── CanSignupGuildRunsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CanSignupGuildRunsAsync_returns_true_for_non_zero_rank_by_default()
    {
        var guild = MakeGuildDoc(members: [
            new BlizzardGuildRosterMember(
                Character: new BlizzardGuildRosterMemberCharacter(
                    Name: "Member",
                    Realm: new BlizzardGuildRosterRealm(Slug: "silvermoon"),
                    Id: 1),
                Rank: 5)
        ]);
        var sut = MakeSut(guild);

        var result = await sut.CanSignupGuildRunsAsync(MakeRaiderDoc(characterName: "Member"), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task CanSignupGuildRunsAsync_honours_explicit_deny_entry()
    {
        var guild = MakeGuildDoc(
            members: [
                new BlizzardGuildRosterMember(
                    Character: new BlizzardGuildRosterMemberCharacter(
                        Name: "Initiate",
                        Realm: new BlizzardGuildRosterRealm(Slug: "silvermoon"),
                        Id: 1),
                    Rank: 7)
            ],
            rankPermissions: [
                new GuildRankPermission(Rank: 7, CanCreateGuildRuns: false, CanSignupGuildRuns: false, CanDeleteGuildRuns: false)
            ]);
        var sut = MakeSut(guild);

        var result = await sut.CanSignupGuildRunsAsync(MakeRaiderDoc(characterName: "Initiate"), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CanSignupGuildRunsAsync_returns_false_for_stale_roster_even_when_default_is_true()
    {
        var guild = MakeGuildDoc(rosterFetchedAt: DateTimeOffset.UtcNow.AddHours(-2));
        var sut = MakeSut(guild);

        var result = await sut.CanSignupGuildRunsAsync(MakeRaiderDoc(), CancellationToken.None);

        Assert.False(result);
    }

    // ─── CanDeleteGuildRunsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CanDeleteGuildRunsAsync_returns_true_for_rank_zero_by_default()
    {
        var sut = MakeSut(MakeGuildDoc());

        var result = await sut.CanDeleteGuildRunsAsync(MakeRaiderDoc(characterName: "Gm"), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task CanDeleteGuildRunsAsync_returns_false_for_non_zero_rank_by_default()
    {
        var guild = MakeGuildDoc(members: [
            new BlizzardGuildRosterMember(
                Character: new BlizzardGuildRosterMemberCharacter(
                    Name: "Officer",
                    Realm: new BlizzardGuildRosterRealm(Slug: "silvermoon"),
                    Id: 1),
                Rank: 1)
        ]);
        var sut = MakeSut(guild);

        var result = await sut.CanDeleteGuildRunsAsync(MakeRaiderDoc(characterName: "Officer"), CancellationToken.None);

        Assert.False(result);
    }

    // ─── Stale-roster boundary (1 hour TTL) ─────────────────────────────────

    [Theory]
    [InlineData(59, true)]   // 59 minutes old → fresh
    [InlineData(60, false)]  // exactly 1 hour → stale boundary triggers (TimeSpan >= TimeSpan equality)
    [InlineData(61, false)]  // 61 minutes old → stale
    public async Task CanCreateGuildRunsAsync_treats_one_hour_as_the_stale_boundary(int ageMinutes, bool expected)
    {
        // Pin the boundary at exactly 1 hour: ageMinutes==60 must be considered stale
        // because the production code uses (UtcNow - fetchedAt) >= TimeSpan.FromHours(1).
        var guild = MakeGuildDoc(rosterFetchedAt: DateTimeOffset.UtcNow.AddMinutes(-ageMinutes));
        var sut = MakeSut(guild);

        var result = await sut.CanCreateGuildRunsAsync(MakeRaiderDoc(characterName: "Gm"), CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(59, true)]
    [InlineData(60, false)]
    [InlineData(61, false)]
    public async Task CanSignupGuildRunsAsync_treats_one_hour_as_the_stale_boundary(int ageMinutes, bool expected)
    {
        var guild = MakeGuildDoc(rosterFetchedAt: DateTimeOffset.UtcNow.AddMinutes(-ageMinutes));
        var sut = MakeSut(guild);

        var result = await sut.CanSignupGuildRunsAsync(MakeRaiderDoc(characterName: "Gm"), CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(59, true)]
    [InlineData(60, false)]
    [InlineData(61, false)]
    public async Task CanDeleteGuildRunsAsync_treats_one_hour_as_the_stale_boundary(int ageMinutes, bool expected)
    {
        var guild = MakeGuildDoc(rosterFetchedAt: DateTimeOffset.UtcNow.AddMinutes(-ageMinutes));
        var sut = MakeSut(guild);

        var result = await sut.CanDeleteGuildRunsAsync(MakeRaiderDoc(characterName: "Gm"), CancellationToken.None);

        Assert.Equal(expected, result);
    }

    // ─── Mixed explicit-vs-default rank entries ─────────────────────────────

    [Fact]
    public async Task CanCreateGuildRunsAsync_falls_back_to_default_when_rank_has_no_explicit_entry()
    {
        // Rank 5 has no permission entry — must fall back to the default rule
        // (rank 0 only). Locks down the FirstOrDefault → default code path that
        // mutation testing flagged at GuildPermissions L99.
        var guild = MakeGuildDoc(
            members: [
                new BlizzardGuildRosterMember(
                    Character: new BlizzardGuildRosterMemberCharacter(
                        Name: "Member",
                        Realm: new BlizzardGuildRosterRealm(Slug: "silvermoon"),
                        Id: 1),
                    Rank: 5)
            ],
            rankPermissions: [
                // Explicit entries for rank 0 and rank 2 only — rank 5 is not listed.
                new GuildRankPermission(Rank: 0, CanCreateGuildRuns: true, CanSignupGuildRuns: true, CanDeleteGuildRuns: true),
                new GuildRankPermission(Rank: 2, CanCreateGuildRuns: true, CanSignupGuildRuns: true, CanDeleteGuildRuns: false),
            ]);
        var sut = MakeSut(guild);

        var result = await sut.CanCreateGuildRunsAsync(MakeRaiderDoc(characterName: "Member"), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CanSignupGuildRunsAsync_falls_back_to_default_when_rank_has_no_explicit_entry()
    {
        // Same shape — for signup the default is true for any matched rank.
        var guild = MakeGuildDoc(
            members: [
                new BlizzardGuildRosterMember(
                    Character: new BlizzardGuildRosterMemberCharacter(
                        Name: "Member",
                        Realm: new BlizzardGuildRosterRealm(Slug: "silvermoon"),
                        Id: 1),
                    Rank: 5)
            ],
            rankPermissions: [
                new GuildRankPermission(Rank: 0, CanCreateGuildRuns: true, CanSignupGuildRuns: true, CanDeleteGuildRuns: true),
                new GuildRankPermission(Rank: 2, CanCreateGuildRuns: false, CanSignupGuildRuns: false, CanDeleteGuildRuns: false),
            ]);
        var sut = MakeSut(guild);

        var result = await sut.CanSignupGuildRunsAsync(MakeRaiderDoc(characterName: "Member"), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task CanDeleteGuildRunsAsync_falls_back_to_default_when_rank_has_no_explicit_entry()
    {
        var guild = MakeGuildDoc(
            members: [
                new BlizzardGuildRosterMember(
                    Character: new BlizzardGuildRosterMemberCharacter(
                        Name: "Member",
                        Realm: new BlizzardGuildRosterRealm(Slug: "silvermoon"),
                        Id: 1),
                    Rank: 5)
            ],
            rankPermissions: [
                new GuildRankPermission(Rank: 0, CanCreateGuildRuns: true, CanSignupGuildRuns: true, CanDeleteGuildRuns: true),
            ]);
        var sut = MakeSut(guild);

        var result = await sut.CanDeleteGuildRunsAsync(MakeRaiderDoc(characterName: "Member"), CancellationToken.None);

        Assert.False(result);
    }
}
