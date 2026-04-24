// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Json;
using Lfm.Contracts.Characters;
using Lfm.Contracts.Me;

namespace Lfm.App.Services;

public sealed class MeClient(IHttpClientFactory factory) : IMeClient
{
    public async Task<MeResponse?> GetAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            return await http.GetFromJsonAsync<MeResponse>("api/v1/me", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return null;
        }
    }

    public async Task<UpdateMeResponse?> UpdateAsync(UpdateMeRequest request, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            // Transitional: send If-Match: * so the server knows the caller is
            // aware of the ETag contract but does not yet round-trip the
            // server-issued value. A future slice will capture the ETag on the
            // preceding GET /api/me and echo it here for full optimistic
            // concurrency. `*` matches any non-deleted resource per RFC 9110.
            using var patch = new HttpRequestMessage(HttpMethod.Patch, "api/v1/me")
            {
                Content = JsonContent.Create(request),
            };
            patch.Headers.TryAddWithoutValidation("If-Match", "*");

            var response = await http.SendAsync(patch, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<UpdateMeResponse>(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return null;
        }
    }

    public async Task<bool> SelectCharacterAsync(string id, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            var response = await http.PutAsync($"api/v1/raider/characters/{id}", content: null, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return false;
        }
    }

    public async Task<bool> DeleteAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            var response = await http.DeleteAsync("api/v1/me", ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<CharacterDto?> EnrichCharacterAsync(string id, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            var response = await http.PostAsync($"api/v1/raider/characters/{id}/enrich", content: null, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<CharacterDto>(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return null;
        }
    }
}
