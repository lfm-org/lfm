// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.i18n;

using System.Globalization;
using Lfm.Contracts.Localization;

public static class SupportedLocales
{
    public const string English = SupportedAppLocales.English;
    public const string Finnish = SupportedAppLocales.Finnish;
    public const string Default = SupportedAppLocales.Default;

    public static IReadOnlyList<string> All => SupportedAppLocales.Codes;

    public static bool Contains(string? locale) =>
        SupportedAppLocales.Contains(locale);

    public static string NormalizeOrDefault(string? locale) =>
        SupportedAppLocales.NormalizeOrDefault(locale);

    public static string MatchBrowserLanguageOrDefault(string? browserLanguage) =>
        SupportedAppLocales.MatchBrowserLanguageOrDefault(browserLanguage);

    public static string LabelKeyOrDefault(string? locale) =>
        SupportedAppLocales.LabelKeyOrDefault(locale);

    public static CultureInfo CultureOrDefault(string? locale) =>
        CultureInfo.GetCultureInfo(SupportedAppLocales.CultureNameOrDefault(locale));

    public static bool IsRtl(string? locale) =>
        SupportedAppLocales.IsRtl(locale);
}
