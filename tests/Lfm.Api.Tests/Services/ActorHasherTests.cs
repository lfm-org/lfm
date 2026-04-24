// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Services;
using Xunit;

namespace Lfm.Api.Tests.Services;

public class ActorHasherTests
{
    [Fact]
    public void IdentityHasher_returns_raw_id()
    {
        var hasher = new IdentityActorHasher();

        Assert.Equal("bnet-1", hasher.Hash("bnet-1"));
    }

    [Fact]
    public void IdentityHasher_substitutes_empty_with_dash()
    {
        var hasher = new IdentityActorHasher();

        Assert.Equal("-", hasher.Hash(""));
        Assert.Equal("-", hasher.Hash(null));
    }

    [Fact]
    public void HmacHasher_produces_lowercase_hex_sha256_length()
    {
        using var hasher = new HmacActorHasher("super-secret-salt");

        var result = hasher.Hash("bnet-42");

        Assert.Equal(64, result.Length); // 32 bytes → 64 hex chars
        Assert.Matches("^[0-9a-f]+$", result);
    }

    [Fact]
    public void HmacHasher_is_deterministic_per_salt()
    {
        using var a = new HmacActorHasher("salt-one");
        using var b = new HmacActorHasher("salt-one");

        Assert.Equal(a.Hash("bnet-42"), b.Hash("bnet-42"));
    }

    [Fact]
    public void HmacHasher_produces_distinct_output_per_salt()
    {
        using var a = new HmacActorHasher("salt-one");
        using var b = new HmacActorHasher("salt-two");

        Assert.NotEqual(a.Hash("bnet-42"), b.Hash("bnet-42"));
    }

    [Fact]
    public void HmacHasher_substitutes_empty_with_dash()
    {
        using var hasher = new HmacActorHasher("salt");

        Assert.Equal("-", hasher.Hash(""));
        Assert.Equal("-", hasher.Hash(null));
    }

    [Fact]
    public void HmacHasher_rejects_empty_salt()
    {
        Assert.Throws<ArgumentException>(() => new HmacActorHasher(""));
    }
}
