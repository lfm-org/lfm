// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.i18n;

/// <summary>
/// Singleton locale tracker. Default is "en"; callers may switch to any
/// supported locale ("en", "fi") via <see cref="SetLocale"/>.
/// </summary>
public sealed class LocaleService : ILocaleService
{
    public string CurrentLocale { get; private set; } = SupportedLocales.Default;

    public event Action? OnLocaleChanged;

    public void SetLocale(string locale)
    {
        if (!SupportedLocales.Contains(locale))
            return;

        var normalized = SupportedLocales.NormalizeOrDefault(locale);
        if (normalized == CurrentLocale)
            return;

        CurrentLocale = normalized;
        OnLocaleChanged?.Invoke();
    }
}
