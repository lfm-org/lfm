// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Json;
using Lfm.Contracts.Guild;

namespace Lfm.App.Services;

public sealed class GuildClient(IHttpClientFactory factory) : IGuildClient
{
    private string? _etag;
    private readonly Dictionary<string, string?> _adminEtags = new(StringComparer.Ordinal);

    public async Task<GuildDto?> GetAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            using var response = await http.GetAsync("api/v1/guild", ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var guild = await response.Content.ReadFromJsonAsync<GuildDto>(ct);
            _etag = guild is null ? null : HttpEtag.Read(response);
            return guild;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<GuildDto?> UpdateAsync(UpdateGuildRequest request, CancellationToken ct)
    {
        var http = factory.CreateClient("api");

        using var patch = new HttpRequestMessage(HttpMethod.Patch, "api/v1/guild")
        {
            Content = JsonContent.Create(request),
        };
        if (!string.IsNullOrWhiteSpace(_etag))
            patch.Headers.TryAddWithoutValidation("If-Match", _etag);

        var response = await http.SendAsync(patch, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var guild = await response.Content.ReadFromJsonAsync<GuildDto>(ct);
        _etag = guild is null ? null : HttpEtag.Read(response);
        return guild;
    }

    public async Task<GuildDto?> GetAdminAsync(string guildId, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            using var response = await http.GetAsync(AdminPath(guildId), ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var guild = await response.Content.ReadFromJsonAsync<GuildDto>(ct);
            _adminEtags[guildId] = guild is null ? null : HttpEtag.Read(response);
            return guild;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<GuildDto?> UpdateAdminAsync(string guildId, UpdateGuildRequest request, CancellationToken ct)
    {
        var http = factory.CreateClient("api");

        using var patch = new HttpRequestMessage(HttpMethod.Patch, AdminPath(guildId))
        {
            Content = JsonContent.Create(request),
        };
        if (_adminEtags.TryGetValue(guildId, out var etag) && !string.IsNullOrWhiteSpace(etag))
            patch.Headers.TryAddWithoutValidation("If-Match", etag);

        var response = await http.SendAsync(patch, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var guild = await response.Content.ReadFromJsonAsync<GuildDto>(ct);
        _adminEtags[guildId] = guild is null ? null : HttpEtag.Read(response);
        return guild;
    }

    private static string AdminPath(string guildId) =>
        $"api/v1/guild/admin?guildId={Uri.EscapeDataString(guildId)}";
}
