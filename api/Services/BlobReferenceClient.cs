// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Runtime.CompilerServices;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Newtonsoft.Json;

namespace Lfm.Api.Services;

/// <inheritdoc />
public sealed class BlobReferenceClient(BlobContainerClient container) : IBlobReferenceClient
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        // Legacy TS-ingested blobs use camelCase at the top level
        // (e.g. "modeKey"). Matching is case-insensitive by default in Newtonsoft,
        // but Blizzard fields use snake_case ("playable_class"). The records we
        // deserialize into carry explicit [JsonProperty] attributes where needed.
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
    };

    public async Task<T?> GetAsync<T>(string blobName, CancellationToken ct) where T : class
    {
        var blob = container.GetBlobClient(blobName);
        string json;
        try
        {
            var response = await blob.DownloadContentAsync(ct);
            json = response.Value.Content.ToString();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(json, JsonSettings);
        }
        catch (JsonException)
        {
            // A blob whose JSON shape does not match T is treated the same as
            // a missing blob. The legacy TS ingester wrote Blizzard's raw
            // index response at reference/{kind}/index.json (an object with
            // _links + an instances array) — not the Phase 3 manifest format
            // (a flat List<IndexEntry>). Returning null lets InstancesRepository
            // and SpecializationsRepository fall through to the per-id
            // enumeration path instead of 500-ing the endpoint.
            return null;
        }
    }

    public async IAsyncEnumerable<T> ListAsync<T>(
        string prefix,
        [EnumeratorCancellation] CancellationToken ct) where T : class
    {
        await foreach (var blob in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, ct))
        {
            if (IsManifestBlob(blob.Name)) continue;

            var value = await GetAsync<T>(blob.Name, ct);
            if (value is not null)
                yield return value;
        }
    }

    private static bool IsManifestBlob(string blobName)
        => blobName.EndsWith("/index.json", StringComparison.Ordinal)
        || blobName.EndsWith("/meta.json", StringComparison.Ordinal);
}
