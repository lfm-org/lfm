// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Json;
using Lfm.Contracts.Instances;

namespace Lfm.App.Services;

public sealed class InstancesClient(IHttpClientFactory factory) : IInstancesClient
{
    public async Task<IReadOnlyList<InstanceDto>> ListAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var items = await http.GetFromJsonAsync<List<InstanceDto>>("api/v1/wow/reference/instances", ct);
        return items ?? [];
    }
}
