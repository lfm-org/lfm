// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Media;
using Lfm.Contracts.WoW;

namespace Lfm.App.Services;

public static class WowMediaUrls
{
    public static string? ToCachedUrl(string? sourceUrl, string? apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return null;

        if (!BlizzardMediaCache.IsBlizzardRenderUrl(sourceUrl))
            return sourceUrl;

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
            return null;

        return $"{apiBaseUrl.TrimEnd('/')}/api/v1/wow/media/cache?source={BlizzardMediaCache.EncodeSource(sourceUrl)}";
    }

    public static string? ClassIconUrl(int classId, string? apiBaseUrl)
    {
        var path = WowClasses.GetIconPath(classId);
        return path is null
            ? null
            : ToCachedUrl($"https://{BlizzardMediaCache.RenderHost}/{path}", apiBaseUrl);
    }
}
