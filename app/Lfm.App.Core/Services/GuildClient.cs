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
            return await http.GetFromJsonAsync<GuildDto>("api/guild", ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<GuildDto?> UpdateAsync(UpdateGuildRequest request, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var response = await http.PatchAsJsonAsync("api/guild", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<GuildDto>(ct);
    }
}
