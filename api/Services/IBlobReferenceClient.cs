// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Services;

/// <summary>
/// Reads JSON blobs from the static Blizzard reference container
/// (<c>lfmstore/wow/</c> in production, Azurite locally).
///
/// See <c>docs/storage-architecture.md</c> for the store split — static Blizzard
/// reference data lives here; dynamic per-user/guild/run data lives in Cosmos.
/// </summary>
public interface IBlobReferenceClient
{
    /// <summary>
    /// Reads a single JSON blob by name and deserializes it to <typeparamref name="T"/>
    /// using Newtonsoft so that <see cref="Lfm.Api.Serialization.LocalizedStringConverter"/>
    /// attributes on <typeparamref name="T"/>'s properties are honored.
    /// Returns null when the blob does not exist (the reader treats 404 as empty).
    /// </summary>
    Task<T?> GetAsync<T>(string blobName, CancellationToken ct) where T : class;

    /// <summary>
    /// Enumerates every blob under <paramref name="prefix"/>, deserializing each one,
    /// skipping manifest-shaped blobs (<c>index.json</c>, <c>meta.json</c>) so callers
    /// receive only per-id detail documents.
    /// </summary>
    IAsyncEnumerable<T> ListAsync<T>(string prefix, CancellationToken ct) where T : class;

    /// <summary>
    /// Serializes <paramref name="payload"/> with Newtonsoft and uploads it to the
    /// container at <paramref name="blobName"/>, overwriting any existing blob.
    /// Used by the ingester (<c>ReferenceSync</c>) to write per-id reference
    /// documents and the list-endpoint manifest (<c>reference/{kind}/index.json</c>).
    /// </summary>
    Task UploadAsync<T>(string blobName, T payload, CancellationToken ct);
}
