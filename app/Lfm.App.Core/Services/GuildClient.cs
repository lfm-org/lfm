// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Json;
using Lfm.Contracts.Guild;

namespace Lfm.App.Services;

public sealed class GuildClient(IHttpClientFactory factory) : IGuildClient
{
    public async Task<GuildDto?> GetAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            return await http.GetFromJsonAsync<GuildDto>("api/v1/guild", ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<GuildDto?> UpdateAsync(UpdateGuildRequest request, CancellationToken ct)
    {
        var http = factory.CreateClient("api");

        // Transitional: send If-Match: * so the server knows the caller is
        // aware of the ETag contract but does not yet round-trip the
        // server-issued value. A future slice will capture the ETag on the
        // preceding GET /api/guild and echo it here for full optimistic
        // concurrency. `*` matches any non-deleted resource per RFC 9110.
        using var patch = new HttpRequestMessage(HttpMethod.Patch, "api/v1/guild")
        {
            Content = JsonContent.Create(request),
        };
        patch.Headers.TryAddWithoutValidation("If-Match", "*");

        var response = await http.SendAsync(patch, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<GuildDto>(ct);
    }
}
