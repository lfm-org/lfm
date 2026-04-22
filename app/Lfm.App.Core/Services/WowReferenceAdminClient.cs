// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Json;
using System.Text.Json;
using Lfm.Contracts.Admin;

namespace Lfm.App.Services;

public sealed class WowReferenceAdminClient(IHttpClientFactory factory) : IWowReferenceAdminClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public async Task<WowReferenceRefreshResponse> RefreshAsync(
        CancellationToken ct,
        IProgress<WowReferenceRefreshProgress>? progress = null)
    {
        // Use the long-timeout "api-admin" client: a cold-cache Blizzard
        // refresh takes minutes (one HTTP call per journal-instance and
        // playable-specialization, rate-limited at ~80 rps). The regular
        // "api" client's 10 s ceiling is fine for the GET endpoints but
        // would kill this POST mid-sync.
        var http = factory.CreateClient("api-admin");

        // ResponseHeadersRead — return as soon as the headers arrive so we
        // can start reading NDJSON lines while the server is still producing them.
        using var response = await http.PostAsync("api/wow/reference/refresh", content: null, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        WowReferenceRefreshResponse? final = null;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            using var doc = JsonDocument.Parse(line);
            var type = doc.RootElement.GetProperty("type").GetString();
            if (type == "progress")
            {
                if (progress is null) continue;
                progress.Report(new WowReferenceRefreshProgress(
                    Entity: doc.RootElement.GetProperty("entity").GetString() ?? "",
                    Phase: doc.RootElement.GetProperty("phase").GetString() ?? "",
                    Processed: doc.RootElement.GetProperty("processed").GetInt32(),
                    Total: doc.RootElement.GetProperty("total").GetInt32(),
                    Current: doc.RootElement.TryGetProperty("current", out var cur) ? cur.GetString() : null,
                    Status: doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() : null));
            }
            else if (type == "done")
            {
                final = doc.RootElement.GetProperty("response")
                    .Deserialize<WowReferenceRefreshResponse>(JsonOptions);
            }
        }

        return final ?? new WowReferenceRefreshResponse([]);
    }
}
