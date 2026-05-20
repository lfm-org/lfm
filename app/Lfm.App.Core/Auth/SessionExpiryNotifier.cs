// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.Auth;

public sealed class SessionExpiryNotifier : ISessionExpiryNotifier
{
    public event Action? SessionExpired;

    public void NotifySessionExpired() => SessionExpired?.Invoke();
}
