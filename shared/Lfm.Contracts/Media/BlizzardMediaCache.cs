// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text;

namespace Lfm.Contracts.Media;

/// <summary>
/// Shared helpers for routing Blizzard render-CDN assets through LFM's media
/// cache instead of linking browser image tags directly to Blizzard.
/// </summary>
public static class BlizzardMediaCache
{
    public const string RenderHost = "render.worldofwarcraft.com";

    public static bool IsBlizzardRenderUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && IsAllowedRenderUri(uri);

    public static string EncodeSource(string sourceUrl)
    {
        var bytes = Encoding.UTF8.GetBytes(sourceUrl);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecodeSource(string encodedSource, out Uri? sourceUri)
    {
        sourceUri = null;
        if (string.IsNullOrWhiteSpace(encodedSource))
            return false;

        var base64 = encodedSource.Replace('-', '+').Replace('_', '/');
        var padding = base64.Length % 4;
        if (padding > 0)
            base64 = base64.PadRight(base64.Length + 4 - padding, '=');

        try
        {
            var bytes = Convert.FromBase64String(base64);
            var source = Encoding.UTF8.GetString(bytes);
            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri))
                return false;

            if (!IsAllowedRenderUri(uri))
                return false;

            sourceUri = uri;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsAllowedRenderUri(Uri uri)
        => string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && string.Equals(uri.Host, RenderHost, StringComparison.OrdinalIgnoreCase)
        && string.IsNullOrEmpty(uri.UserInfo);
}
