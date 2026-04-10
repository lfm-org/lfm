using FluentAssertions;
using Lfm.Api.Auth;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace Lfm.Api.Tests;

public class DataProtectionSessionCipherTests
{
    private static DataProtectionSessionCipher MakeSut() =>
        new(new EphemeralDataProtectionProvider());

    private static SessionPrincipal MakePrincipal(string battleNetId = "bnet-123") =>
        new(
            BattleNetId: battleNetId,
            BattleTag: "Player#1234",
            GuildId: "42",
            GuildName: "Test Guild",
            IssuedAt: new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero),
            ExpiresAt: new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Protect_then_Unprotect_roundtrips_every_field()
    {
        var sut = MakeSut();
        var original = MakePrincipal();

        var encrypted = sut.Protect(original);
        var decrypted = sut.Unprotect(encrypted);

        decrypted.Should().NotBeNull();
        decrypted!.BattleNetId.Should().Be(original.BattleNetId);
        decrypted.BattleTag.Should().Be(original.BattleTag);
        decrypted.GuildId.Should().Be(original.GuildId);
        decrypted.GuildName.Should().Be(original.GuildName);
        decrypted.IssuedAt.Should().Be(original.IssuedAt);
        decrypted.ExpiresAt.Should().Be(original.ExpiresAt);
    }

    [Fact]
    public void Protect_produces_opaque_string_that_does_not_contain_plaintext()
    {
        var sut = MakeSut();
        var principal = MakePrincipal(battleNetId: "bnet-secret");

        var encrypted = sut.Protect(principal);

        encrypted.Should().NotContain("bnet-secret",
            "the protected payload must not leak plaintext fields");
        encrypted.Should().NotContain("Player#1234");
    }

    [Fact]
    public void Protect_is_non_deterministic_across_invocations()
    {
        var sut = MakeSut();
        var principal = MakePrincipal();

        var first = sut.Protect(principal);
        var second = sut.Protect(principal);

        first.Should().NotBe(second,
            "Data Protection outputs must not be byte-identical for repeated protect calls");
    }

    [Fact]
    public void Unprotect_returns_null_for_garbage_payload()
    {
        var sut = MakeSut();

        var result = sut.Unprotect("not-a-real-token");

        result.Should().BeNull("malformed payloads must not throw and must return null");
    }

    [Fact]
    public void Unprotect_returns_null_for_empty_payload()
    {
        var sut = MakeSut();

        var result = sut.Unprotect(string.Empty);

        result.Should().BeNull();
    }

    [Fact]
    public void Unprotect_returns_null_when_tampered()
    {
        var sut = MakeSut();
        var encrypted = sut.Protect(MakePrincipal());

        // Flip a character near the middle — this breaks the authenticated payload.
        var tampered = encrypted[..(encrypted.Length / 2)]
            + (encrypted[encrypted.Length / 2] == 'A' ? 'B' : 'A')
            + encrypted[(encrypted.Length / 2 + 1)..];

        var result = sut.Unprotect(tampered);

        result.Should().BeNull("tampered tokens must fail authenticated decryption");
    }

    [Fact]
    public void Unprotect_with_token_from_different_provider_returns_null()
    {
        // Different ephemeral providers derive independent keys — decrypting a
        // token from provider A with provider B must fail.
        var cipherA = MakeSut();
        var cipherB = MakeSut();
        var encrypted = cipherA.Protect(MakePrincipal());

        var result = cipherB.Unprotect(encrypted);

        result.Should().BeNull();
    }
}
