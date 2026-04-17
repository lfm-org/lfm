// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Xunit;

namespace Lfm.Api.Tests;

public class GuildResolverTests
{
    [Fact]
    public void FromRaider_SelectedCharacterWithGuild_ReturnsGuild()
    {
        var raider = new RaiderDocument("r1", "r1", "eu-realm-char", null,
            Characters: [new StoredSelectedCharacter("eu-realm-char", "eu", "realm", "Char", GuildId: 99, GuildName: "Sisu")]);

        var (guildId, guildName) = GuildResolver.FromRaider(raider);

        Assert.Equal("99", guildId);
        Assert.Equal("Sisu", guildName);
    }

    [Fact]
    public void FromRaider_NullRaider_ReturnsNulls()
    {
        var (guildId, guildName) = GuildResolver.FromRaider(null);
        Assert.Null(guildId);
        Assert.Null(guildName);
    }

    [Fact]
    public void FromRaider_NoSelectedCharacter_ReturnsNulls()
    {
        var raider = new RaiderDocument("r1", "r1", null, null,
            Characters: [new StoredSelectedCharacter("eu-realm-char", "eu", "realm", "Char", GuildId: 99, GuildName: "Sisu")]);

        var (guildId, guildName) = GuildResolver.FromRaider(raider);
        Assert.Null(guildId);
        Assert.Null(guildName);
    }

    /// <summary>
    /// A selected character must have a non-null <see cref="StoredSelectedCharacter.GuildId"/>
    /// for the resolver to return a guild. A dangling <see cref="StoredSelectedCharacter.GuildName"/>
    /// without a <c>GuildId</c> must not leak as a ghost guild — returning a name without
    /// an id would surface a fake guild in /api/me responses.
    /// </summary>
    [Theory]
    [InlineData(null, null)]   // fully absent
    [InlineData(null, "Sisu")] // strict: name without id must not leak
    public void FromRaider_SelectedCharacterWithoutGuildId_ReturnsNulls(int? storedGuildId, string? storedGuildName)
    {
        var raider = new RaiderDocument("r1", "r1", "eu-realm-char", null,
            Characters: [new StoredSelectedCharacter(
                Id: "eu-realm-char",
                Region: "eu",
                Realm: "realm",
                Name: "Char",
                GuildId: storedGuildId,
                GuildName: storedGuildName)]);

        var (guildId, guildName) = GuildResolver.FromRaider(raider);

        Assert.Null(guildId);
        Assert.Null(guildName);
    }
}
