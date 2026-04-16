// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Globalization;

namespace Lfm.App.Tests;

/// <summary>
/// WCAG 2.x relative-luminance and contrast-ratio math for hex sRGB colors.
/// Used by accessibility regression tests to lock palette tokens against the
/// 1.4.3 (text contrast ≥ 4.5:1) and 1.4.11 (non-text contrast ≥ 3:1) bars.
/// Formula: https://www.w3.org/TR/WCAG22/#dfn-relative-luminance
/// </summary>
public static class ColorContrast
{
    /// <summary>
    /// Computes the contrast ratio (≥ 1.0, ≤ 21.0) between two sRGB colors
    /// passed as 6-digit hex strings (with or without a leading <c>#</c>).
    /// Order is irrelevant — the function always divides lighter by darker.
    /// </summary>
    public static double Ratio(string hexA, string hexB)
    {
        var la = RelativeLuminance(hexA);
        var lb = RelativeLuminance(hexB);
        var lighter = Math.Max(la, lb);
        var darker = Math.Min(la, lb);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Per WCAG: linearized R/G/B channels weighted 0.2126/0.7152/0.0722.
    /// </summary>
    public static double RelativeLuminance(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        return 0.2126 * Linearize(r) + 0.7152 * Linearize(g) + 0.0722 * Linearize(b);
    }

    private static (double r, double g, double b) ParseHex(string hex)
    {
        var trimmed = hex.TrimStart('#');
        if (trimmed.Length != 6)
            throw new ArgumentException($"Expected a 6-digit hex color, got '{hex}'.", nameof(hex));

        var r = int.Parse(trimmed.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var g = int.Parse(trimmed.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var b = int.Parse(trimmed.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        return (r, g, b);
    }

    private static double Linearize(double channel) =>
        channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
}
