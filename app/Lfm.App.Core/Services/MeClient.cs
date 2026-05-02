// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Net.Http.Json;
using Lfm.Contracts.Characters;
using Lfm.Contracts.Me;

namespace Lfm.App.Services;

public sealed class MeClient(IHttpClientFactory factory) : IMeClient
{
    private const int GetMeMaxAttempts = 4;
    private string? _etag;

    public async Task<MeResponse?> GetAsync(CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        for (var attempt = 1; attempt <= GetMeMaxAttempts; attempt++)
        {
            try
            {
                using var response = await http.GetAsync("api/v1/me", ct);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    _etag = null;
                    return null;
                }
                if (response.IsSuccessStatusCode)
                {
                    var me = await response.Content.ReadFromJsonAsync<MeResponse>(cancellationToken: ct);
                    _etag = me is null ? null : HttpEtag.Read(response);
                    return me;
                }
                if (!IsTransientStatus(response.StatusCode) || attempt == GetMeMaxAttempts)
                    return null;
            }
            catch (Exception ex) when (IsTransientGetException(ex, ct) && attempt < GetMeMaxAttempts)
            {
                // Retry below.
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                return null;
            }

            try
            {
                await Task.Delay(GetMeRetryDelay(attempt), ct);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return null;
    }

    private static bool IsTransientStatus(HttpStatusCode statusCode)
        => statusCode is
            HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;

    private static bool IsTransientGetException(Exception ex, CancellationToken ct)
        => !ct.IsCancellationRequested &&
           ex is HttpRequestException or TaskCanceledException or OperationCanceledException;

    private static TimeSpan GetMeRetryDelay(int attempt)
        => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1));

    public async Task<UpdateMeResponse?> UpdateAsync(UpdateMeRequest request, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            using var patch = new HttpRequestMessage(HttpMethod.Patch, "api/v1/me")
            {
                Content = JsonContent.Create(request),
            };
            if (!string.IsNullOrWhiteSpace(_etag))
                patch.Headers.TryAddWithoutValidation("If-Match", _etag);

            var response = await http.SendAsync(patch, ct);
            if (!response.IsSuccessStatusCode) return null;
            var updated = await response.Content.ReadFromJsonAsync<UpdateMeResponse>(cancellationToken: ct);
            _etag = updated is null ? null : HttpEtag.Read(response);
            return updated;
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
