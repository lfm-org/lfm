// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Security.Cryptography;
using System.Text;
using Lfm.Contracts.Media;
using Microsoft.Extensions.Logging;

namespace Lfm.Api.Services;

public sealed class WowMediaCache(
    IBlobReferenceClient blobs,
    HttpClient httpClient,
    ILogger<WowMediaCache> logger) : IWowMediaCache
{
    private const int MaxImageBytes = 5 * 1024 * 1024;

    public async Task<ReferenceBlobContent?> GetOrFetchAsync(string encodedSource, CancellationToken ct)
    {
        if (!BlizzardMediaCache.TryDecodeSource(encodedSource, out var sourceUri) || sourceUri is null)
            return null;

        var blobName = BlobNameFor(sourceUri);
        var cached = await blobs.GetContentAsync(blobName, ct);
        if (cached is not null)
            return cached;

        using var response = await httpClient.GetAsync(sourceUri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Could not fetch Blizzard media asset {Source}: status {StatusCode}",
                sourceUri,
                (int)response.StatusCode);
            return null;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(contentType)
            || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Rejected Blizzard media asset {Source}: content type {ContentType}",
                sourceUri,
                contentType ?? "<missing>");
            return null;
        }

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength > MaxImageBytes)
        {
            logger.LogWarning(
                "Rejected Blizzard media asset {Source}: content length {ContentLength} exceeds {MaxImageBytes}",
                sourceUri,
                contentLength,
                MaxImageBytes);
            return null;
        }

        var content = await response.Content.ReadAsByteArrayAsync(ct);
        if (content.Length > MaxImageBytes)
        {
            logger.LogWarning(
                "Rejected Blizzard media asset {Source}: content size {ContentLength} exceeds {MaxImageBytes}",
                sourceUri,
                content.Length,
                MaxImageBytes);
            return null;
        }

        await blobs.UploadContentAsync(blobName, content, contentType, ct);
        return new ReferenceBlobContent(content, contentType);
    }

    internal static string BlobNameFor(Uri sourceUri)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceUri.AbsoluteUri)))
            .ToLowerInvariant();
        var extension = Path.GetExtension(sourceUri.AbsolutePath);
        if (extension.Length is 0 or > 10)
            extension = ".img";

        return $"media-cache/render/{hash}{extension}";
    }
}
