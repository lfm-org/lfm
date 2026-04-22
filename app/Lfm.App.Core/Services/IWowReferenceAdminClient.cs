// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Admin;

namespace Lfm.App.Services;

public interface IWowReferenceAdminClient
{
    /// <summary>
    /// Triggers a full Blizzard reference-data refresh. Site-admin only on
    /// the server. Returns the per-entity result table.
    /// Throws <see cref="HttpRequestException"/> on transport failure or
    /// any non-2xx response (the admin page surfaces this inline).
    /// </summary>
    Task<WowReferenceRefreshResponse> RefreshAsync(CancellationToken ct);
}
