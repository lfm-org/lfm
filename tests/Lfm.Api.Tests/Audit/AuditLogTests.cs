// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Audit;
using Lfm.Api.Services;
using Xunit;

namespace Lfm.Api.Tests.Audit;

/// <summary>
/// Regression tests for the actor-hashing behaviour installed by
/// <see cref="AuditLog.ConfigureHasher"/>. These tests are collection-serial
/// because <see cref="AuditLog"/> holds the hasher as a static field; running
/// them in parallel with other tests that inspect audit output would race.
/// </summary>
[Collection(nameof(AuditLogHasherCollection))]
public class AuditLogTests : IDisposable
{
    public AuditLogTests()
    {
        // Snapshot — not exposed, so we reinstall the default (identity) after.
    }

    public void Dispose()
    {
        // Restore the identity hasher so tests in other files continue to see
        // the plaintext actor id they assert on today.
        AuditLog.ConfigureHasher(new IdentityActorHasher());
    }

    [Fact]
    public void Emit_logs_hashed_actor_when_HmacHasher_installed()
    {
        using var hasher = new HmacActorHasher("audit-test-salt");
        AuditLog.ConfigureHasher(hasher);

        var logger = new TestLogger<AuditLogTests>();
        AuditLog.Emit(logger, new AuditEvent("test.action", "bnet-42", "target-7", "success", null));

        var entry = Assert.Single(logger.Entries);
        var actorId = (string)entry.Properties["AuditActorId"]!;
        Assert.Equal(64, actorId.Length);
        Assert.Matches("^[0-9a-f]+$", actorId);
        Assert.NotEqual("bnet-42", actorId);
    }

    [Fact]
    public void Emit_logs_raw_actor_under_IdentityHasher_default()
    {
        AuditLog.ConfigureHasher(new IdentityActorHasher());

        var logger = new TestLogger<AuditLogTests>();
        AuditLog.Emit(logger, new AuditEvent("test.action", "bnet-42", null, "success", null));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("bnet-42", entry.Properties["AuditActorId"]);
    }
}

[CollectionDefinition(nameof(AuditLogHasherCollection), DisableParallelization = true)]
public sealed class AuditLogHasherCollection
{
    // Marker — mutating the AuditLog static hasher must not run in parallel
    // with any other audit-log assertions. xUnit collections that opt in via
    // [Collection(nameof(AuditLogHasherCollection))] are serialized.
}
