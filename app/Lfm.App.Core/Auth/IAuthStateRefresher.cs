// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.Auth;

/// <summary>
/// Narrow seam for pages that mutate data mirrored into authentication claims.
/// </summary>
public interface IAuthStateRefresher
{
    void RefreshAuthenticationState();
}
