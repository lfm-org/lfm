// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.i18n;

public static class SupportedLocales
{
    public const string English = "en";
    public const string Finnish = "fi";
    public const string Default = English;

    private static readonly HashSet<string> Values = new(StringComparer.OrdinalIgnoreCase)
    {
        English,
        Finnish,
    };

    public static IReadOnlyList<string> All { get; } = [English, Finnish];

    public static bool Contains(string? locale) =>
        !string.IsNullOrWhiteSpace(locale) && Values.Contains(locale);

    public static string NormalizeOrDefault(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return Default;

        var normalized = locale.ToLowerInvariant();
        return Values.Contains(normalized) ? normalized : Default;
    }
}
