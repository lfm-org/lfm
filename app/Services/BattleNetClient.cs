using System.Net.Http.Json;
using Lfm.Contracts.Characters;

namespace Lfm.App.Services;

public sealed class BattleNetClient(IHttpClientFactory factory) : IBattleNetClient
{
    public async Task<IReadOnlyList<CharacterDto>?> GetCharactersAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            return await http.GetFromJsonAsync<List<CharacterDto>>("api/battlenet/characters", ct);
        }
        catch (HttpRequestException)
        {
            return null;
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
            return await response.Content.ReadFromJsonAsync<List<CharacterDto>>(ct);
        }
        catch (HttpRequestException)
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
            var result = await response.Content.ReadFromJsonAsync<PortraitResponse>(ct);
            return result?.Portraits.ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
