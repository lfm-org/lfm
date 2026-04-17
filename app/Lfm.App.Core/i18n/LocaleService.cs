// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.i18n;

/// <summary>
/// Singleton locale tracker. Default is "en"; callers may switch to any
/// supported locale ("en", "fi") via <see cref="SetLocale"/>.
/// </summary>
public sealed class LocaleService : ILocaleService
{
    private static readonly HashSet<string> SupportedLocales = new(StringComparer.OrdinalIgnoreCase)
    {
        "en",
        "fi",
    };

    public string CurrentLocale { get; private set; } = "en";

    public event Action? OnLocaleChanged;

    public void SetLocale(string locale)
    {
        if (!SupportedLocales.Contains(locale))
            return;

        var normalized = locale.ToLowerInvariant();
        if (normalized == CurrentLocale)
            return;

        CurrentLocale = normalized;
        OnLocaleChanged?.Invoke();
    }
}
