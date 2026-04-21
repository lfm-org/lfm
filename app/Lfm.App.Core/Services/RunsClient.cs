// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Json;
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
        var response = await http.GetFromJsonAsync<RunsListResponse>("api/runs", ct);
        return response?.Items ?? [];
    }

    public async Task<RunDetailDto?> GetAsync(string id, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        return await http.GetFromJsonAsync<RunDetailDto>($"api/runs/{Uri.EscapeDataString(id)}", ct);
    }

    public async Task<RunDetailDto?> CreateAsync(CreateRunRequest request, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var response = await http.PostAsJsonAsync("api/runs", request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<RunDetailDto>(ct);
    }

    public async Task<RunDetailDto?> UpdateAsync(string id, UpdateRunRequest request, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var response = await http.PutAsJsonAsync($"api/runs/{Uri.EscapeDataString(id)}", request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<RunDetailDto>(ct);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var response = await http.DeleteAsync($"api/runs/{Uri.EscapeDataString(id)}", ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<RunDetailDto?> SignupAsync(string runId, SignupRequest request, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var response = await http.PostAsJsonAsync($"api/runs/{Uri.EscapeDataString(runId)}/signup", request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<RunDetailDto>(ct);
    }

    public async Task<RunDetailDto?> CancelSignupAsync(string runId, CancellationToken ct)
    {
        var http = factory.CreateClient("api");
        var response = await http.DeleteAsync($"api/runs/{Uri.EscapeDataString(runId)}/signup", ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<RunDetailDto>(ct);
    }
}
