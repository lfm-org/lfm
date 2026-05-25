// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Json;
using Lfm.Contracts.Specializations;

namespace Lfm.App.Services;

public sealed class SpecializationsClient(IHttpClientFactory factory) : ISpecializationsClient
{
    private readonly ReferenceListCache<SpecializationDto> _cache = new();

    public Task<IReadOnlyList<SpecializationDto>> ListAsync(CancellationToken ct) =>
        _cache.GetOrLoadAsync(FetchAsync, ct);

    private async Task<IReadOnlyList<SpecializationDto>> FetchAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var items = await http.GetFromJsonAsync<List<SpecializationDto>>("api/v1/wow/reference/specializations", ct);
        return items ?? [];
    }
}
