// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Localization;

internal sealed record AppLocaleInfo(
    string Code,
    string LabelKey,
    string CultureName,
    bool IsRtl,
    IReadOnlyList<string> BrowserLanguagePrefixes);

public static class SupportedAppLocales
{
    public const string English = "en";
    public const string Finnish = "fi";
    public const string Default = English;

    private static readonly AppLocaleInfo[] Locales =
    [
        new(English, "locale.en", "en-GB", false, [English]),
        new(Finnish, "locale.fi", "fi-FI", false, [Finnish]),
    ];

    private static readonly HashSet<string> Values = new(
        Locales.Select(l => l.Code),
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Codes { get; } =
        Locales.Select(l => l.Code).ToArray();

    public static bool Contains(string? locale) =>
        !string.IsNullOrWhiteSpace(locale) && Values.Contains(locale.Trim());

    public static string NormalizeOrDefault(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return Default;

        var normalized = locale.Trim().ToLowerInvariant();
        return Values.Contains(normalized) ? normalized : Default;
    }

    public static string LabelKeyOrDefault(string? locale) =>
        ResolveOrDefault(locale).LabelKey;

    public static string CultureNameOrDefault(string? locale) =>
        ResolveOrDefault(locale).CultureName;

    public static bool IsRtl(string? locale) =>
        ResolveOrDefault(locale).IsRtl;

    private static AppLocaleInfo ResolveOrDefault(string? locale)
    {
        var normalized = NormalizeOrDefault(locale);
        return Locales.First(l => string.Equals(l.Code, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static string MatchBrowserLanguageOrDefault(string? browserLanguage)
    {
        if (string.IsNullOrWhiteSpace(browserLanguage))
            return Default;

        var normalized = browserLanguage.Trim().ToLowerInvariant().Replace('_', '-');
        return Locales.FirstOrDefault(l => l.BrowserLanguagePrefixes.Any(prefix =>
            normalized.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase)))?.Code
            ?? Default;
    }
}
