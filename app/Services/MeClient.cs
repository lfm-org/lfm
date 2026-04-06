using System.Net.Http.Json;
using Lfm.Contracts.Me;

namespace Lfm.App.Services;

public sealed class MeClient(IHttpClientFactory factory) : IMeClient
{
    public async Task<MeResponse?> GetAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            return await http.GetFromJsonAsync<MeResponse>("api/me", ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
