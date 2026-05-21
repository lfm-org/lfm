// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Media;
using Microsoft.AspNetCore.Http;

namespace Lfm.Api.Helpers;

internal static class ApiMediaUrls
{
    internal static string? ToCachedUrl(HttpRequest req, string? sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return null;

        if (!BlizzardMediaCache.IsBlizzardRenderUrl(sourceUrl))
            return sourceUrl;

        var baseUrl = $"{req.Scheme}://{req.Host}";
        return $"{baseUrl}/api/v1/wow/media/cache?source={BlizzardMediaCache.EncodeSource(sourceUrl)}";
    }
}
