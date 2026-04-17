// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Azure;
using Lfm.Api.Options;
using Lfm.Api.Services;
using Moq;
using MsOptions = Microsoft.Extensions.Options.Options;
using Xunit;

namespace Lfm.Api.Tests;

public class SiteAdminServiceTests
{
    private const string TestVaultUrl = "https://kv.example.net/";
    private const string SecretName = "site-admin-battle-net-ids";

    private static SiteAdminService MakeSut(string? keyVaultUrl = null, ISecretResolver? secretResolver = null) =>
        new(
            MsOptions.Create(new AuthOptions
            {
                CookieName = "battlenet_token",
                KeyVaultUrl = keyVaultUrl,
            }),
            secretResolver ?? Mock.Of<ISecretResolver>());

    [Fact]
    public async Task IsAdminAsync_returns_false_when_battle_net_id_is_empty()
    {
        // Defensive: an empty caller id must short-circuit and never reach the secret store.
        var sut = MakeSut(keyVaultUrl: "https://kv.example.net/");

        var result = await sut.IsAdminAsync(string.Empty, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_when_battle_net_id_is_null_safe_string()
    {
        // SiteAdminService takes a non-null string; this exercises the IsNullOrEmpty guard
        // for the empty case (null is enforced by the parameter type).
        var sut = MakeSut(keyVaultUrl: "https://kv.example.net/");

        var result = await sut.IsAdminAsync("", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_when_key_vault_url_is_null()
    {
        var sut = MakeSut(keyVaultUrl: null);

        var result = await sut.IsAdminAsync("player#1234", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_when_key_vault_url_is_empty()
    {
        var sut = MakeSut(keyVaultUrl: "");

        var result = await sut.IsAdminAsync("player#1234", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_when_key_vault_url_is_whitespace()
    {
        var sut = MakeSut(keyVaultUrl: "   ");

        var result = await sut.IsAdminAsync("player#1234", CancellationToken.None);

        Assert.False(result);
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

        Assert.False(first);
        Assert.False(second);
        Assert.False(third);
    }

    // ── Key Vault fetch path ────────────────────────────────────────────────

    private static Mock<ISecretResolver> ResolverReturning(string? value)
    {
        var mock = new Mock<ISecretResolver>(MockBehavior.Strict);
        mock.Setup(r => r.GetSecretAsync(TestVaultUrl, SecretName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value);
        return mock;
    }

    [Fact]
    public async Task IsAdminAsync_returns_true_when_secret_contains_caller_id()
    {
        var resolver = ResolverReturning("player#1234\nadmin#9999");
        var sut = MakeSut(keyVaultUrl: TestVaultUrl, secretResolver: resolver.Object);

        var result = await sut.IsAdminAsync("player#1234", CancellationToken.None);

        Assert.True(result);
        resolver.Verify(
            r => r.GetSecretAsync(TestVaultUrl, SecretName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_when_secret_does_not_contain_caller_id()
    {
        var resolver = ResolverReturning("other#0001\nother#0002");
        var sut = MakeSut(keyVaultUrl: TestVaultUrl, secretResolver: resolver.Object);

        var result = await sut.IsAdminAsync("player#1234", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsAdminAsync_caches_result_within_ttl_window()
    {
        var resolver = ResolverReturning("player#1234");
        var sut = MakeSut(keyVaultUrl: TestVaultUrl, secretResolver: resolver.Object);

        await sut.IsAdminAsync("player#1234", CancellationToken.None);
        await sut.IsAdminAsync("player#1234", CancellationToken.None);

        resolver.Verify(
            r => r.GetSecretAsync(TestVaultUrl, SecretName, It.IsAny<CancellationToken>()),
            Times.Once,
            "two calls within the 60-second cache window must only fetch the secret once");
    }

    [Fact]
    public async Task IsAdminAsync_parses_comma_separated_secret_values()
    {
        var resolver = ResolverReturning("a, b, c");
        var sut = MakeSut(keyVaultUrl: TestVaultUrl, secretResolver: resolver.Object);

        Assert.True(await sut.IsAdminAsync("a", CancellationToken.None));
        Assert.True(await sut.IsAdminAsync("b", CancellationToken.None));
        Assert.True(await sut.IsAdminAsync("c", CancellationToken.None));
    }

    [Fact]
    public async Task IsAdminAsync_parses_newline_separated_secret_values()
    {
        var resolver = ResolverReturning("a\nb\nc");
        var sut = MakeSut(keyVaultUrl: TestVaultUrl, secretResolver: resolver.Object);

        Assert.True(await sut.IsAdminAsync("a", CancellationToken.None));
        Assert.True(await sut.IsAdminAsync("b", CancellationToken.None));
        Assert.True(await sut.IsAdminAsync("c", CancellationToken.None));
    }

    [Fact]
    public async Task IsAdminAsync_trims_whitespace_around_ids()
    {
        var resolver = ResolverReturning("  player#1234  \n  admin#9999  ");
        var sut = MakeSut(keyVaultUrl: TestVaultUrl, secretResolver: resolver.Object);

        Assert.True(await sut.IsAdminAsync("player#1234", CancellationToken.None));
        Assert.True(await sut.IsAdminAsync("admin#9999", CancellationToken.None));
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_when_secret_is_null()
    {
        var resolver = ResolverReturning(null);
        var sut = MakeSut(keyVaultUrl: TestVaultUrl, secretResolver: resolver.Object);

        var result = await sut.IsAdminAsync("player#1234", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsAdminAsync_returns_false_when_resolver_throws_and_no_prior_cache()
    {
        var resolver = new Mock<ISecretResolver>(MockBehavior.Strict);
        resolver.Setup(r => r.GetSecretAsync(TestVaultUrl, SecretName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("kv down"));
        var sut = MakeSut(keyVaultUrl: TestVaultUrl, secretResolver: resolver.Object);

        var result = await sut.IsAdminAsync("player#1234", CancellationToken.None);

        Assert.False(result);
    }

    // Not tested: the catch-branch "extend stale cache and return cached.Ids" path in
    // GetAdminIdsAsync. Exercising it would require forcing the 60-second TTL to expire
    // between calls, which needs a TimeProvider seam in the SUT. Judged not worth a
    // production seam for one branch-coverage point on a hobby project.
}
