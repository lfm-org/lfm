// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Lfm.App.Services;

public sealed class CredentialsHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // Include cookies on all requests — required when the API is on a different
        // origin (different port) from the Blazor WASM app.
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return base.SendAsync(request, ct);
    }
}
