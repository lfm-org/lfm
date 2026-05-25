// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Json;
using Lfm.Contracts.Expansions;

namespace Lfm.App.Services;

public sealed class ExpansionsClient(IHttpClientFactory factory) : IExpansionsClient
{
    private readonly ReferenceListCache<ExpansionDto> _cache = new();

    public Task<IReadOnlyList<ExpansionDto>> ListAsync(CancellationToken ct) =>
        _cache.GetOrLoadAsync(FetchAsync, ct);

    private async Task<IReadOnlyList<ExpansionDto>> FetchAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var items = await http.GetFromJsonAsync<List<ExpansionDto>>("api/v1/wow/reference/expansions", ct);
        return items ?? [];
    }
}
