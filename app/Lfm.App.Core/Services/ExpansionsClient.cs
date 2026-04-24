// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Json;
using Lfm.Contracts.Expansions;

namespace Lfm.App.Services;

public sealed class ExpansionsClient(IHttpClientFactory factory) : IExpansionsClient
{
    public async Task<IReadOnlyList<ExpansionDto>> ListAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var items = await http.GetFromJsonAsync<List<ExpansionDto>>("api/v1/wow/reference/expansions", ct);
        return items ?? [];
    }
}
