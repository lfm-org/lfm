// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Json;
using Lfm.Contracts.Guild;

namespace Lfm.App.Services;

public sealed class GuildClient(IHttpClientFactory factory) : IGuildClient, IDisposable
{
    private static readonly TimeSpan CurrentGuildCacheTtl = TimeSpan.FromMinutes(10);
    private const int MaxAdminEtags = 32;

    private string? _etag;
    private GuildDto? _currentGuild;
    private DateTimeOffset _currentGuildExpiresAt;
    private bool _hasCurrentGuild;
    private readonly IDataCache? _dataCache;
    private readonly Dictionary<string, AdminEtagEntry> _adminEtags = new(StringComparer.Ordinal);
    private TimeProvider _timeProvider = TimeProvider.System;
    private long _adminEtagSequence;

    public GuildClient(IHttpClientFactory factory, IDataCache dataCache)
        : this(factory, dataCache, TimeProvider.System)
    {
    }

    public GuildClient(IHttpClientFactory factory, TimeProvider timeProvider)
        : this(factory)
    {
        _timeProvider = timeProvider;
    }

    public GuildClient(IHttpClientFactory factory, IDataCache dataCache, TimeProvider timeProvider)
        : this(factory, timeProvider)
    {
        _dataCache = dataCache;
        _dataCache.OnInvalidated += HandleCacheInvalidated;
    }

    public async Task<GuildDto?> GetAsync(CancellationToken ct)
    {
        if (_hasCurrentGuild && _currentGuildExpiresAt > _timeProvider.GetUtcNow())
            return _currentGuild;

        ClearCurrentGuild();

        var http = factory.CreateClient("api");
        try
        {
            using var response = await http.GetAsync("api/v1/guild", ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var guild = await response.Content.ReadFromJsonAsync<GuildDto>(ct);
            _etag = guild is null ? null : HttpEtag.Read(response);
            if (guild is not null)
            {
                StoreCurrentGuild(guild);
            }
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
        if (guild is null)
            ClearCurrentGuild();
        else
            StoreCurrentGuild(guild);
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
            StoreAdminEtag(guildId, guild is null ? null : HttpEtag.Read(response));
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
        if (_adminEtags.TryGetValue(guildId, out var etag) && !string.IsNullOrWhiteSpace(etag.Value))
            patch.Headers.TryAddWithoutValidation("If-Match", etag.Value);

        var response = await http.SendAsync(patch, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var guild = await response.Content.ReadFromJsonAsync<GuildDto>(ct);
        StoreAdminEtag(guildId, guild is null ? null : HttpEtag.Read(response));
        return guild;
    }

    private static string AdminPath(string guildId) =>
        $"api/v1/guild/admin?guildId={Uri.EscapeDataString(guildId)}";

    private void StoreCurrentGuild(GuildDto guild)
    {
        _currentGuild = guild;
        _currentGuildExpiresAt = _timeProvider.GetUtcNow().Add(CurrentGuildCacheTtl);
        _hasCurrentGuild = true;
    }

    private void ClearCurrentGuild()
    {
        _etag = null;
        _currentGuild = null;
        _currentGuildExpiresAt = default;
        _hasCurrentGuild = false;
    }

    private void StoreAdminEtag(string guildId, string? etag)
    {
        if (string.IsNullOrWhiteSpace(etag))
        {
            _adminEtags.Remove(guildId);
            return;
        }

        _adminEtags[guildId] = new AdminEtagEntry(etag, ++_adminEtagSequence);
        TrimAdminEtags();
    }

    private void TrimAdminEtags()
    {
        while (_adminEtags.Count > MaxAdminEtags)
        {
            var oldest = _adminEtags.MinBy(pair => pair.Value.Sequence).Key;
            _adminEtags.Remove(oldest);
        }
    }

    private void HandleCacheInvalidated(string key)
    {
        if (key != DataCacheKeys.Guild)
            return;

        ClearCurrentGuild();
    }

    public void Dispose()
    {
        if (_dataCache is not null)
            _dataCache.OnInvalidated -= HandleCacheInvalidated;
    }

    private sealed record AdminEtagEntry(string Value, long Sequence);
}
