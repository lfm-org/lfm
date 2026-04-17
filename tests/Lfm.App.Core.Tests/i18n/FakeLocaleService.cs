// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.App.i18n;

namespace Lfm.App.Core.Tests.i18n;

/// <summary>
/// Test-only <see cref="ILocaleService"/> that exposes the event invocation list
/// so tests can pin subscribe/unsubscribe behavior deterministically.
/// </summary>
internal sealed class FakeLocaleService : ILocaleService
{
    public string CurrentLocale { get; private set; } = "en";

    public event Action? OnLocaleChanged;

    public void SetLocale(string locale)
    {
        CurrentLocale = locale;
        OnLocaleChanged?.Invoke();
    }

    public int SubscriberCount => OnLocaleChanged?.GetInvocationList().Length ?? 0;
}
