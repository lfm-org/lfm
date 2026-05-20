// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Lfm.App.Auth;

namespace Lfm.App.Services;

public sealed class CredentialsHandler(ISessionExpiryNotifier sessionExpiryNotifier) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // Include cookies on all requests — required when the API is on a different
        // origin (different port) from the Blazor WASM app.
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        var response = await base.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            sessionExpiryNotifier.NotifySessionExpired();

        return response;
    }
}
