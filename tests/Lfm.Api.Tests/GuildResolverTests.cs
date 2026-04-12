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

    [Fact]
    public void FromRaider_SelectedCharacterNoGuild_ReturnsNulls()
    {
        var raider = new RaiderDocument("r1", "r1", "eu-realm-char", null,
            Characters: [new StoredSelectedCharacter("eu-realm-char", "eu", "realm", "Char")]);

        var (guildId, guildName) = GuildResolver.FromRaider(raider);
        Assert.Null(guildId);
        Assert.Null(guildName);
    }
}
