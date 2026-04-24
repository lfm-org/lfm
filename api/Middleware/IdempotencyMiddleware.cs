// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Diagnostics;
using System.Text.Json;
using Lfm.Api.Helpers;
using Lfm.Api.Options;
using Lfm.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Middleware;

/// <summary>
/// Durable replay for retried mutations. When a mutating request
/// (POST / PUT / PATCH / DELETE) carries an <c>Idempotency-Key</c> header
/// and a resolved session principal, the middleware:
///
/// <list type="bullet">
///   <item>Looks up <c>(battleNetId, idempotencyKey)</c> in <see cref="IIdempotencyStore"/>.</item>
///   <item>On hit, short-circuits with 200 + a small problem+json body telling the caller
///   the request was already processed and advising a follow-up GET for the current state
///   (the simplest-correct first-iteration design from the plan).</item>
///   <item>On miss, runs the handler, and if it responds with 2xx persists a minimal entry
///   so a later retry replays. Non-2xx responses are not stored — retrying a 4xx/5xx is the
///   caller's responsibility and should return the current server answer, not a cached failure.</item>
/// </list>
///
/// Anonymous requests, non-mutating methods, and requests without an
/// <c>Idempotency-Key</c> header pass through untouched.
/// </summary>
public sealed class IdempotencyMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly HashSet<string> MutatingMethods =
        new(["POST", "PUT", "PATCH", "DELETE"], StringComparer.OrdinalIgnoreCase);

    private const string HeaderName = "Idempotency-Key";
    private const string ReplayMarkerHeader = "Idempotent-Replay";

    private readonly IIdempotencyStore _store;
    private readonly IdempotencyOptions _options;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(
        IIdempotencyStore store,
        IOptions<IdempotencyOptions> options,
        ILogger<IdempotencyMiddleware> logger)
    {
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is null)
        {
            await next(context);
            return;
        }

        if (!MutatingMethods.Contains(httpContext.Request.Method))
        {
            await next(context);
            return;
        }

        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            await next(context);
            return;
        }

        var key = values.ToString();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 255)
        {
            // Never trust an oversized or empty key — reject with 400 rather
            // than writing a probe into Cosmos or attempting a replay lookup.
            await WriteBadKeyAsync(httpContext, key);
            return;
        }

        var principal = context.TryGetPrincipal();
        if (principal is null)
        {
            // Unauthenticated — the downstream auth policy will reject. No
            // idempotency state is written for anonymous callers because we
            // have no stable partition key for them.
            await next(context);
            return;
        }

        var existing = await _store.TryGetAsync(principal.BattleNetId, key, httpContext.RequestAborted);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Idempotency replay for {BattleNetId} key={Key} originalStatus={Status}",
                principal.BattleNetId, key, existing.StatusCode);
            await WriteReplayHintAsync(httpContext, existing);
            return;
        }

        await next(context);

        var status = httpContext.Response.StatusCode;
        if (status is >= 200 and < 300)
        {
            var etag = httpContext.Response.Headers.ETag.ToString();
            var entry = new IdempotencyEntry(
                Id: IdempotencyStore.DocumentId(principal.BattleNetId, key),
                BattleNetId: principal.BattleNetId,
                IdempotencyKey: key,
                StatusCode: status,
                ETag: string.IsNullOrEmpty(etag) ? null : etag,
                BodyHash: null,
                CreatedAt: DateTimeOffset.UtcNow.ToString("o"),
                Ttl: _options.TtlSeconds);

            try
            {
                await _store.PutAsync(entry, httpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                // Storing is best-effort — if Cosmos blinks we still want the
                // successful response to reach the client. The next retry just
                // won't short-circuit.
                _logger.LogWarning(ex, "Failed to persist idempotency entry for {BattleNetId}", principal.BattleNetId);
            }
        }
    }

    private static async Task WriteReplayHintAsync(HttpContext httpContext, IdempotencyEntry entry)
    {
        // Do not mirror the original response body (we never stored it). We
        // mirror the status code so the client sees "200 OK" / "201 Created"
        // and deduces the operation succeeded, and we set a marker header so
        // observant clients can detect a replay without parsing the body.
        httpContext.Response.StatusCode = entry.StatusCode;
        httpContext.Response.Headers[ReplayMarkerHeader] = "true";
        if (!string.IsNullOrEmpty(entry.ETag))
            httpContext.Response.Headers.ETag = entry.ETag;

        httpContext.Response.ContentType = Problem.ContentType;
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = $"{Problem.TypeBase}#idempotent-replay",
            Title = "Idempotent replay",
            Status = entry.StatusCode,
            Detail = "The original request already completed. GET the resource for its current state.",
            Instance = httpContext.Request.Path.Value,
        };
        problem.Extensions["originalCreatedAt"] = entry.CreatedAt;
        var traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrEmpty(traceId))
            problem.Extensions["traceId"] = traceId;

        await JsonSerializer.SerializeAsync(httpContext.Response.Body, problem);
    }

    private static async Task WriteBadKeyAsync(HttpContext httpContext, string key)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        httpContext.Response.ContentType = Problem.ContentType;
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = $"{Problem.TypeBase}#invalid-idempotency-key",
            Title = "Bad Request",
            Status = StatusCodes.Status400BadRequest,
            Detail = string.IsNullOrWhiteSpace(key)
                ? "Idempotency-Key header must not be empty."
                : "Idempotency-Key header must be at most 255 characters.",
            Instance = httpContext.Request.Path.Value,
        };
        var traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrEmpty(traceId))
            problem.Extensions["traceId"] = traceId;

        await JsonSerializer.SerializeAsync(httpContext.Response.Body, problem);
    }
}
