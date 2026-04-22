// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Admin;

namespace Lfm.App.Services;

public interface IWowReferenceAdminClient
{
    /// <summary>
    /// Triggers a full Blizzard reference-data refresh. Site-admin only on
    /// the server. Streams per-entity progress events through
    /// <paramref name="progress"/> (one event per processed item plus
    /// <c>start</c> / <c>end</c> markers) and returns the final per-entity
    /// result table when the server closes the stream.
    ///
    /// Throws <see cref="HttpRequestException"/> on transport failure or any
    /// non-2xx response (the admin page surfaces this inline).
    /// </summary>
    /// <param name="progress">Optional progress sink. Events fire on the HTTP
    /// response-reader's thread; UI consumers must marshal back to the render
    /// loop via <c>InvokeAsync(StateHasChanged)</c>.</param>
    Task<WowReferenceRefreshResponse> RefreshAsync(
        CancellationToken ct,
        IProgress<WowReferenceRefreshProgress>? progress = null);
}
