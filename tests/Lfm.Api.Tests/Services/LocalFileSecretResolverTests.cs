// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Services;
using Xunit;

namespace Lfm.Api.Tests.Services;

public sealed class LocalFileSecretResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lfm-local-secrets-{Guid.NewGuid():N}");

    public LocalFileSecretResolverTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task GetSecretAsync_reads_secret_from_file_uri_root()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "site-admin-battle-net-ids"), "player-1\nplayer-2");
        var sut = new LocalFileSecretResolver();

        var value = await sut.GetSecretAsync(new Uri(_root).AbsoluteUri, "site-admin-battle-net-ids", CancellationToken.None);

        Assert.Equal("player-1\nplayer-2", value);
    }

    [Fact]
    public async Task GetSecretAsync_accepts_root_uri_with_trailing_separator()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "site-admin-battle-net-ids"), "player-1");
        var rootUri = new Uri(_root + Path.DirectorySeparatorChar).AbsoluteUri;
        var sut = new LocalFileSecretResolver();

        var value = await sut.GetSecretAsync(rootUri, "site-admin-battle-net-ids", CancellationToken.None);

        Assert.Equal("player-1", value);
    }

    [Fact]
    public async Task GetSecretAsync_returns_null_for_missing_secret()
    {
        var sut = new LocalFileSecretResolver();

        var value = await sut.GetSecretAsync(new Uri(_root).AbsoluteUri, "missing", CancellationToken.None);

        Assert.Null(value);
    }

    [Fact]
    public async Task GetSecretAsync_rejects_path_traversal_secret_names()
    {
        var sut = new LocalFileSecretResolver();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetSecretAsync(new Uri(_root).AbsoluteUri, "../outside", CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
