// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lfm.App.Runs;
using Lfm.Contracts.Runs;

namespace Lfm.App.Services;

public sealed class RunsClient(IHttpClientFactory factory) : IRunsClient
{
    public async Task<IReadOnlyList<RunSummaryDto>> ListAsync(CancellationToken ct)
    {
        // The server returns a RunsListResponse envelope capped at 200 items per
        // page with a continuation token for further pages. The SPA currently
        // consumes only the first page — a future "load more" feature will need
        // a ListAsync overload that exposes ContinuationToken.
        var http = factory.CreateClient("api");
        var response = await http.GetFromJsonAsync<RunsListResponse>("api/v1/runs", ct);
        return response?.Items ?? [];
    }

    public async Task<RunDetailDto?> GetAsync(string id, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        return await http.GetFromJsonAsync<RunDetailDto>($"api/v1/runs/{Uri.EscapeDataString(id)}", ct);
    }

    public async Task<RunDetailWithEtag?> GetWithEtagAsync(string id, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        using var response = await http.GetAsync($"api/v1/runs/{Uri.EscapeDataString(id)}", ct);
        if (!response.IsSuccessStatusCode) return null;

        var dto = await response.Content.ReadFromJsonAsync<RunDetailDto>(ct);
        if (dto is null) return null;

        var etag = ReadETag(response);
        return new RunDetailWithEtag(dto, etag);
    }

    public async Task<RunDetailDto?> CreateAsync(CreateRunRequest request, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var response = await http.PostAsJsonAsync("api/v1/runs", request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<RunDetailDto>(ct);
    }

    public async Task<RunDetailWithEtag?> UpdateAsync(
        string id, UpdateRunRequest request, string ifMatchEtag, CancellationToken ct)
    {
        var http = factory.CreateClient("api");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"api/v1/runs/{Uri.EscapeDataString(id)}")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.TryAddWithoutValidation("If-Match", ifMatchEtag);

        using var response = await http.SendAsync(httpRequest, ct);
        if (response.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            // The server sends problem+json on 412; surface it to the caller via
            // a dedicated exception so the page can offer a reload prompt rather
            // than the generic "save failed" inline error.
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new StaleEtagException(body);
        }
        if (!response.IsSuccessStatusCode) return null;

        var dto = await response.Content.ReadFromJsonAsync<RunDetailDto>(ct);
        if (dto is null) return null;

        return new RunDetailWithEtag(dto, ReadETag(response));
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var response = await http.DeleteAsync($"api/v1/runs/{Uri.EscapeDataString(id)}", ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<RunDetailDto?> SignupAsync(string runId, SignupRequest request, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var response = await http.PostAsJsonAsync($"api/v1/runs/{Uri.EscapeDataString(runId)}/signup", request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<RunDetailDto>(ct);
    }

    public async Task<CharactersFetchResult> GetSignupOptionsAsync(string runId, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        try
        {
            var response = await http.GetAsync($"api/v1/runs/{Uri.EscapeDataString(runId)}/signup/options", ct);
            if (response.StatusCode == HttpStatusCode.NoContent)
                return new CharactersFetchResult.NeedsRefresh();
            if (!response.IsSuccessStatusCode)
                return new CharactersFetchResult.Error();

            var options = await response.Content.ReadFromJsonAsync<RunSignupOptionsDto>(cancellationToken: ct);
            return options is null
                ? new CharactersFetchResult.Error()
                : new CharactersFetchResult.Cached(options.Characters);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return new CharactersFetchResult.Error();
        }
    }

    public async Task<RunDetailDto?> CancelSignupAsync(string runId, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var response = await http.DeleteAsync($"api/v1/runs/{Uri.EscapeDataString(runId)}/signup", ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<RunDetailDto>(ct);
    }

    private static string? ReadETag(HttpResponseMessage response)
    {
        if (response.Headers.ETag?.Tag is { Length: > 0 } typed)
            return typed;

        return response.Headers.TryGetValues("ETag", out var values)
            ? values.FirstOrDefault()
            : null;
    }
}
