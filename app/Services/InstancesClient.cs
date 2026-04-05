using System.Net.Http.Json;

namespace Lfm.App.Services;

public sealed class InstancesClient(IHttpClientFactory factory) : IInstancesClient
{
    public async Task<IReadOnlyList<ClientInstanceRecord>> ListAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var items = await http.GetFromJsonAsync<List<ClientInstanceRecord>>("api/instances", ct);
        return items ?? [];
    }
}
