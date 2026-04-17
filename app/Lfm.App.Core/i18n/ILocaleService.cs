// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.i18n;

/// <summary>
/// Tracks the active UI locale and notifies subscribers on change.
/// </summary>
public interface ILocaleService
{
    /// <summary>Currently active locale code (e.g. "en", "fi").</summary>
    string CurrentLocale { get; }

    /// <summary>Fired after <see cref="SetLocale"/> changes the active locale.</summary>
    event Action? OnLocaleChanged;

    /// <summary>
    /// Switch to <paramref name="locale"/>. Ignored if the value is already active
    /// or not in the supported set.
    /// </summary>
    void SetLocale(string locale);
}
