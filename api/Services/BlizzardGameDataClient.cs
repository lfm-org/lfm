// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lfm.Api.Options;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Services;

/// <summary>
/// Implements <see cref="IBlizzardGameDataClient"/> against the Blizzard Game Data API.
///
/// Mirrors the pattern in reference-sync-blizzard.ts:
///   fetchBlizzardToken  → <see cref="GetClientCredentialsTokenAsync"/>
///   fetchStaticJson     → <see cref="GetStaticJsonAsync{T}"/>
///
/// HttpClient base address: https://{region}.api.blizzard.com/ (set in Program.cs).
/// Token endpoint:          https://{region}.battle.net/oauth/token (separate request).
///
/// JSON deserialization uses camelCase → PascalCase via PropertyNameCaseInsensitive.
/// The Blizzard API uses snake_case for some fields; those are mapped with
/// [JsonPropertyName] on the DTO record properties.
/// </summary>
public sealed class BlizzardGameDataClient : IBlizzardGameDataClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly BlizzardOptions _opts;

    public BlizzardGameDataClient(HttpClient httpClient, IOptions<BlizzardOptions> options)
    {
        _httpClient = httpClient;
        _opts = options.Value;
    }

    /// <inheritdoc/>
    public async Task<string> GetClientCredentialsTokenAsync(CancellationToken ct)
    {
        var region = _opts.Region.ToLowerInvariant();
        var host = region == "cn" ? "gateway.battlenet.com.cn" : $"{region}.battle.net";
        var tokenUrl = $"https://{host}/oauth/token";

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.ClientSecret}"));

        var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var tokenDto = JsonSerializer.Deserialize<TokenEndpointResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("Blizzard token endpoint returned empty response.");
        return tokenDto.AccessToken;
    }

    // ---------------------------------------------------------------------------
    // Index + detail fetchers
    // ---------------------------------------------------------------------------

    /// <inheritdoc/>
    public Task<BlizzardPlayableClassIndex> GetPlayableClassIndexAsync(string accessToken, CancellationToken ct)
        => GetStaticJsonAsync<BlizzardPlayableClassIndex>(
            "data/wow/playable-class/index", accessToken, ct);

    /// <inheritdoc/>
    public Task<BlizzardPlayableClassDetail> GetPlayableClassAsync(int classId, string accessToken, CancellationToken ct)
        => GetStaticJsonAsync<BlizzardPlayableClassDetail>(
            $"data/wow/playable-class/{classId}", accessToken, ct);

    /// <inheritdoc/>
    public Task<BlizzardPlayableSpecIndex> GetPlayableSpecIndexAsync(string accessToken, CancellationToken ct)
        => GetStaticJsonAsync<BlizzardPlayableSpecIndex>(
            "data/wow/playable-specialization/index", accessToken, ct);

    /// <inheritdoc/>
    public Task<BlizzardPlayableSpecDetail> GetPlayableSpecAsync(int specId, string accessToken, CancellationToken ct)
        => GetStaticJsonAsync<BlizzardPlayableSpecDetail>(
            $"data/wow/playable-specialization/{specId}", accessToken, ct);

    /// <inheritdoc/>
    public Task<BlizzardMediaAssets> GetPlayableSpecMediaAsync(int specId, string accessToken, CancellationToken ct)
        => GetStaticJsonAsync<BlizzardMediaAssets>(
            $"data/wow/media/playable-specialization/{specId}", accessToken, ct);

    /// <inheritdoc/>
    public Task<BlizzardJournalInstanceIndex> GetJournalInstanceIndexAsync(string accessToken, CancellationToken ct)
        => GetStaticJsonAsync<BlizzardJournalInstanceIndex>(
            "data/wow/journal-instance/index", accessToken, ct);

    /// <inheritdoc/>
    public Task<BlizzardJournalInstanceDetail> GetJournalInstanceAsync(int instanceId, string accessToken, CancellationToken ct)
        => GetStaticJsonAsync<BlizzardJournalInstanceDetail>(
            $"data/wow/journal-instance/{instanceId}", accessToken, ct);

    // ---------------------------------------------------------------------------
    // Generic static-namespace fetch
    // ---------------------------------------------------------------------------

    private async Task<T> GetStaticJsonAsync<T>(string path, string accessToken, CancellationToken ct)
    {
        var region = _opts.Region.ToLowerInvariant();
        var staticNamespace = $"static-{region}";

        // path is a relative path; HttpClient base address is already set to
        // https://{region}.api.blizzard.com/  in Program.cs.
        var uriBuilder = new UriBuilder(_httpClient.BaseAddress!)
        {
            Path = $"/{path}",
            Query = $"namespace={Uri.EscapeDataString(staticNamespace)}&locale=en_US",
        };

        var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Blizzard API returned empty response for {path}.");
    }

    // ---------------------------------------------------------------------------
    // Private DTOs
    // ---------------------------------------------------------------------------

    private sealed class TokenEndpointResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
    }
}
