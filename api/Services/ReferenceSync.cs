// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Admin;
using Microsoft.Extensions.Logging;

namespace Lfm.Api.Services;

/// <summary>
/// Implements <see cref="IReferenceSync"/>.
///
/// Phase 1 (2026-04-21) moved reference-data *reads* from Cosmos to blob — see
/// <c>docs/storage-architecture.md</c>. The writer side (this class) will be
/// re-implemented in Phase 3 to upload blobs instead of upserting Cosmos
/// documents. In the meantime both entity stubs surface a clear "failed:"
/// status through the admin <c>POST /api/wow/update</c> endpoint, while the
/// respective repositories continue to serve the existing
/// <c>lfmstore/wow/reference/</c> blobs ingested by the legacy TS job.
///
/// Per-entity failures are caught and recorded; the remaining entities are
/// still attempted.
/// </summary>
public sealed class ReferenceSync(ILogger<ReferenceSync> logger) : IReferenceSync
{
    /// <inheritdoc/>
    public Task<WowUpdateResponse> SyncAllAsync(CancellationToken ct)
    {
        var results = new List<WowUpdateEntityResult>();

        foreach (var (name, sync) in Entities)
        {
            try
            {
                sync();
                results.Add(new WowUpdateEntityResult(name, "synced"));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to sync {Entity}", name);
                results.Add(new WowUpdateEntityResult(name, $"failed: {ex.Message}"));
            }
        }

        return Task.FromResult(new WowUpdateResponse(results));
    }

    private static readonly (string Name, Action Sync)[] Entities =
    [
        ("instances", SyncInstancesAsync),
        ("specializations", SyncSpecializationsAsync),
    ];

    private static void SyncInstancesAsync() =>
        throw new NotImplementedException(
            "Instance sync is being rewritten to write blobs in Phase 3. " +
            "Existing lfmstore/wow/reference/journal-instance/ blobs remain readable via InstancesRepository.");

    private static void SyncSpecializationsAsync() =>
        throw new NotImplementedException(
            "Specialization sync is being rewritten to write blobs in Phase 3. " +
            "Existing lfmstore/wow/reference/playable-specialization/ blobs remain readable via SpecializationsRepository.");
}
