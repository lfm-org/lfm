// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.i18n;

using System.Globalization;

/// <summary>
/// Singleton locale tracker. Default is "en"; callers may switch to any
/// supported locale ("en", "fi") via <see cref="SetLocale"/>.
/// </summary>
public sealed class LocaleService : ILocaleService
{
    public string CurrentLocale { get; private set; } = SupportedLocales.Default;

    public event Action? OnLocaleChanged;

    public LocaleService()
    {
        ApplyCulture(CurrentLocale);
    }

    public void SetLocale(string locale)
    {
        if (!SupportedLocales.Contains(locale))
            return;

        var normalized = SupportedLocales.NormalizeOrDefault(locale);
        if (normalized == CurrentLocale)
            return;

        CurrentLocale = normalized;
        ApplyCulture(normalized);
        OnLocaleChanged?.Invoke();
    }

    private static void ApplyCulture(string locale)
    {
        var culture = SupportedLocales.CultureOrDefault(locale);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
