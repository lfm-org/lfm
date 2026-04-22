// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Json;
using Lfm.Contracts.Admin;

namespace Lfm.App.Services;

public sealed class WowReferenceAdminClient(IHttpClientFactory factory) : IWowReferenceAdminClient
{
    public async Task<WowReferenceRefreshResponse> RefreshAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var response = await http.PostAsync("api/wow/reference/refresh", content: null, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<WowReferenceRefreshResponse>(cancellationToken: ct);
        // The server always returns a populated response on 2xx; null here
        // would indicate a protocol-level bug worth surfacing.
        return body ?? new WowReferenceRefreshResponse([]);
    }
}
