// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lfm.Contracts.Characters;

namespace Lfm.App.Services;

public sealed class BattleNetClient(IHttpClientFactory factory) : IBattleNetClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<CharactersFetchResult> GetCharactersAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            var response = await http.GetAsync("api/battlenet/characters", ct);
            if (response.StatusCode == HttpStatusCode.NoContent)
                return new CharactersFetchResult.NeedsRefresh();
            if (!response.IsSuccessStatusCode)
                return new CharactersFetchResult.Error();
            var chars = await response.Content.ReadFromJsonAsync<List<CharacterDto>>(JsonOptions, ct);
            return chars is null
                ? new CharactersFetchResult.Error()
                : new CharactersFetchResult.Cached(chars);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return new CharactersFetchResult.Error();
        }
    }

    public async Task<IReadOnlyList<CharacterDto>?> RefreshCharactersAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            var response = await http.PostAsync("api/battlenet/characters/refresh", null, ct);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<List<CharacterDto>>(JsonOptions, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return null;
        }
    }

    public async Task<IDictionary<string, string>?> GetPortraitsAsync(
        IEnumerable<CharacterPortraitRequest> requests,
        CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            var response = await http.PostAsJsonAsync("api/battlenet/character-portraits", requests, ct);
            if (!response.IsSuccessStatusCode)
                return null;
            var result = await response.Content.ReadFromJsonAsync<PortraitResponse>(JsonOptions, ct);
            return result?.Portraits.ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return null;
        }
    }
}
