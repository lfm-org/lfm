// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Newtonsoft.Json;
using Lfm.Api.Serialization;
using Lfm.Contracts.Instances;

namespace Lfm.Api.Repositories;

/// <summary>
/// Cosmos document stored in the "instances" container.
/// Each document represents one (instance, mode) pair.
/// Partition key: /id  (set to document id = "{instanceId}:{modeKey}").
///
/// instanceId is stored as a string so that SELECT c.instanceId AS id can be
/// projected directly into InstanceDto.Id (string) by ListAsync.
/// </summary>
public sealed record InstanceDocument(
    /// <summary>Cosmos document id and partition key: "{instanceId}:{modeKey}".</summary>
    string Id,
    /// <summary>Blizzard instance id as string (e.g. "67"). Projected as 'id' by ListAsync.</summary>
    string InstanceId,
    [property: JsonConverter(typeof(LocalizedStringConverter))] string Name,
    /// <summary>Mode key, e.g. "NORMAL:25" or "HEROIC:5".</summary>
    string ModeKey,
    string Expansion);

public interface IInstancesRepository
{
    Task<IReadOnlyList<InstanceDto>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Upserts a batch of instance documents (one per mode).
    /// Existing documents with the same id are replaced.
    /// </summary>
    Task UpsertBatchAsync(IEnumerable<InstanceDocument> documents, CancellationToken ct);
}
