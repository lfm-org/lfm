using FluentAssertions;
using Lfm.Api.Options;
using Lfm.Api.Services;
using MsOptions = Microsoft.Extensions.Options.Options;
using Xunit;

namespace Lfm.Api.Tests;

public class SiteAdminServiceTests
{
    private static SiteAdminService MakeSut(string? keyVaultUrl = null) =>
        new(MsOptions.Create(new AuthOptions
        {
            CookieName = "battlenet_token",
            KeyVaultUrl = keyVaultUrl,
        }));

    [Fact]
    public async Task IsAdminAsync_returns_false_when_battle_net_id_is_empty()
    {
        // Defensive: an empty caller id must short-circuit and never reach the secret store.
        var sut = MakeSut(keyVaultUrl: "https://kv.example.net/");

        var result = await sut.IsAdminAsync(string.Empty, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_when_battle_net_id_is_null_safe_string()
    {
        // SiteAdminService takes a non-null string; this exercises the IsNullOrEmpty guard
        // for the empty case (null is enforced by the parameter type).
        var sut = MakeSut(keyVaultUrl: "https://kv.example.net/");

        var result = await sut.IsAdminAsync("", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_when_key_vault_url_is_null()
    {
        var sut = MakeSut(keyVaultUrl: null);

        var result = await sut.IsAdminAsync("player#1234", CancellationToken.None);

        result.Should().BeFalse("KeyVaultUrl is not configured, so the admin set is empty");
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_when_key_vault_url_is_empty()
    {
        var sut = MakeSut(keyVaultUrl: "");

        var result = await sut.IsAdminAsync("player#1234", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_when_key_vault_url_is_whitespace()
    {
        var sut = MakeSut(keyVaultUrl: "   ");

        var result = await sut.IsAdminAsync("player#1234", CancellationToken.None);

        result.Should().BeFalse("KeyVaultUrl is trimmed before the empty check");
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_for_any_id_when_key_vault_url_is_unconfigured()
    {
        // Multiple lookups must all return false from the cached empty set, not
        // accidentally promote anyone when no KV is configured.
        var sut = MakeSut(keyVaultUrl: null);

        var first = await sut.IsAdminAsync("player#1234", CancellationToken.None);
        var second = await sut.IsAdminAsync("admin#9999", CancellationToken.None);
        var third = await sut.IsAdminAsync("any-id", CancellationToken.None);

        first.Should().BeFalse();
        second.Should().BeFalse();
        third.Should().BeFalse();
    }
}
