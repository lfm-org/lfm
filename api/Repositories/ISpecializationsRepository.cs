// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Newtonsoft.Json;
using Lfm.Api.Serialization;
using Lfm.Contracts.Specializations;

namespace Lfm.Api.Repositories;

/// <summary>
/// Cosmos document stored in the "specializations" container.
/// Partition key: /id  (string representation of the numeric spec id).
/// </summary>
public sealed record SpecializationDocument(
    /// <summary>Cosmos document id and partition key: string form of the numeric spec id.</summary>
    string Id,
    int SpecId,
    [property: JsonConverter(typeof(LocalizedStringConverter))] string Name,
    int ClassId,
    string Role,
    string? IconUrl);

public interface ISpecializationsRepository
{
    Task<IReadOnlyList<SpecializationDto>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Upserts a batch of specialization documents.
    /// Existing documents with the same id are replaced.
    /// </summary>
    Task UpsertBatchAsync(IEnumerable<SpecializationDocument> documents, CancellationToken ct);
}
