// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Lfm.Api.Services;
using Lfm.Contracts.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves POST /api/wow/reference/refresh (admin only).
///
/// Streams progress events as <c>application/x-ndjson</c> — one JSON object per
/// line, flushed as the sync moves along. Each line is one of:
/// <list type="bullet">
///   <item><c>{ "type": "progress", "entity", "phase", "processed", "total", "current"?, "status"? }</c></item>
///   <item><c>{ "type": "done", "response": { "results": [...] } }</c> — the final line</item>
/// </list>
/// The admin UI reads the stream incrementally so the user sees
/// "instances 73/510 (Siege of Boralus)" live, instead of a single spinner
/// that turns into a table 1–2 minutes later.
///
/// Auth:
///   - [RequireAuth] → AuthPolicyMiddleware returns 401 for unauthenticated callers.
///   - ISiteAdminService check → 403 for authenticated non-admin callers.
/// </summary>
public class WowReferenceRefreshFunction(ISiteAdminService siteAdmin, IReferenceSync referenceSync)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Function("wow-reference-refresh")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "wow/reference/refresh")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        if (!await siteAdmin.IsAdminAsync(principal.BattleNetId, ct))
            return new ObjectResult(new { error = "Forbidden" }) { StatusCode = 403 };

        var response = req.HttpContext.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "application/x-ndjson";
        response.Headers["Cache-Control"] = "no-cache, no-transform";
        // Hint to intermediate proxies (incl. nginx-based reverse proxies in
        // front of SWA linked backends) to forward bytes instead of buffering.
        response.Headers["X-Accel-Buffering"] = "no";

        // Channel decouples the sync loop (producer, driven by IProgress.Report)
        // from the response-body writer (consumer). Bounded capacity keeps the
        // memory footprint predictable if a slow client backs up the writer.
        var channel = Channel.CreateBounded<WowReferenceRefreshProgress>(
            new BoundedChannelOptions(1024)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            });

        var progress = new ChannelProgress(channel.Writer);

        // Producer: drive SyncAllAsync and close the channel when it finishes
        // (success or fault) so the consumer's ReadAllAsync loop terminates.
        // SyncAllAsync never throws per contract; the try/finally is belt-and-
        // braces against a future refactor.
        var syncTask = Task.Run(async () =>
        {
            try
            {
                return await referenceSync.SyncAllAsync(ct, progress);
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, ct);

        // Consumer: stream each progress event as an NDJSON line, flushing
        // after every line so the client sees updates in real time.
        await foreach (var evt in channel.Reader.ReadAllAsync(ct))
        {
            await WriteNdjsonLineAsync(response, new ProgressEnvelope(evt), ct);
        }

        var result = await syncTask;
        await WriteNdjsonLineAsync(response, new DoneEnvelope(result), ct);

        // Body has been written directly; tell MVC not to write anything else.
        return new EmptyResult();
    }

    private static async Task WriteNdjsonLineAsync(HttpResponse response, object envelope, CancellationToken ct)
    {
        await JsonSerializer.SerializeAsync(response.Body, envelope, envelope.GetType(), JsonOptions, ct);
        await response.Body.WriteAsync(NewLine, ct);
        await response.Body.FlushAsync(ct);
    }

    private static readonly byte[] NewLine = "\n"u8.ToArray();

    private sealed record ProgressEnvelope(
        string Type,
        string Entity,
        string Phase,
        int Processed,
        int Total,
        string? Current,
        string? Status)
    {
        public ProgressEnvelope(WowReferenceRefreshProgress e)
            : this("progress", e.Entity, e.Phase, e.Processed, e.Total, e.Current, e.Status)
        {
        }
    }

    private sealed record DoneEnvelope(string Type, WowReferenceRefreshResponse Response)
    {
        public DoneEnvelope(WowReferenceRefreshResponse response)
            : this("done", response)
        {
        }
    }

    private sealed class ChannelProgress(ChannelWriter<WowReferenceRefreshProgress> writer)
        : IProgress<WowReferenceRefreshProgress>
    {
        // Synchronous write: unlike the default Progress<T>, this does not
        // dispatch to the thread pool. Progress events stay ordered as the
        // sync loop emits them — which matters because the client renders
        // "N of Total" counters that must move monotonically.
        public void Report(WowReferenceRefreshProgress value) => writer.TryWrite(value);
    }
}
