// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Json;
using Lfm.Contracts.Admin;

namespace Lfm.App.Services;

public sealed class WowReferenceAdminClient(IHttpClientFactory factory) : IWowReferenceAdminClient
{
    public async Task<WowReferenceRefreshResponse> RefreshAsync(CancellationToken ct)
    {
        // Use the long-timeout "api-admin" client: a cold-cache Blizzard
        // refresh takes minutes (one HTTP call per journal-instance and
        // playable-specialization, rate-limited at ~80 rps). The regular
        // "api" client's 10 s ceiling is fine for the GET endpoints but
        // would kill this POST mid-sync.
        var http = factory.CreateClient("api-admin");
        var response = await http.PostAsync("api/wow/reference/refresh", content: null, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<WowReferenceRefreshResponse>(cancellationToken: ct);
        // The server always returns a populated response on 2xx; null here
        // would indicate a protocol-level bug worth surfacing.
        return body ?? new WowReferenceRefreshResponse([]);
    }
}
