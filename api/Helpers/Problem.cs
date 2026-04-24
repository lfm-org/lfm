// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Lfm.Api.Helpers;

/// <summary>
/// Factory for RFC 9457 <c>application/problem+json</c> responses. Each
/// helper builds a <see cref="ProblemDetails"/> with a stable <c>type</c> URI
/// rooted at <see cref="TypeBase"/>, attaches the current W3C trace id as an
/// extension for downstream correlation, and returns an <see cref="IActionResult"/>
/// so existing Function handlers can swap <c>new NotFoundObjectResult(new { error = … })</c>
/// for <c>Problem.NotFound(req.HttpContext, "run-not-found", "…")</c> without
/// changing return types.
///
/// Type URIs take the form <c>https://github.com/lfm-org/lfm/errors#&lt;slug&gt;</c>
/// so downstream AGPL operators get a clickable, versionable reference that
/// resolves to an anchor in the project's future error catalogue.
/// </summary>
public static class Problem
{
    /// <summary>
    /// Base URL for problem <c>type</c> fragments. Per RFC 9457 §3.1.1 a
    /// problem's <c>type</c> SHOULD dereference to documentation; this URL
    /// points at the canonical upstream repository so forks that omit an
    /// operator-specific catalogue still have a functioning link.
    /// </summary>
    public const string TypeBase = "https://github.com/lfm-org/lfm/errors";

    /// <summary>
    /// Standard Content-Type for problem responses per RFC 9457 §3.
    /// </summary>
    public const string ContentType = "application/problem+json";

    public static IActionResult NotFound(HttpContext context, string slug, string? detail = null, IDictionary<string, object?>? extensions = null)
        => Create(context, StatusCodes.Status404NotFound, "Not Found", slug, detail, extensions);

    public static IActionResult BadRequest(HttpContext context, string slug, string? detail = null, IDictionary<string, object?>? extensions = null)
        => Create(context, StatusCodes.Status400BadRequest, "Bad Request", slug, detail, extensions);

    public static IActionResult Unauthorized(HttpContext context, string slug, string? detail = null)
        => Create(context, StatusCodes.Status401Unauthorized, "Unauthorized", slug, detail);

    public static IActionResult Forbidden(HttpContext context, string slug, string? detail = null)
        => Create(context, StatusCodes.Status403Forbidden, "Forbidden", slug, detail);

    public static IActionResult Conflict(HttpContext context, string slug, string? detail = null)
        => Create(context, StatusCodes.Status409Conflict, "Conflict", slug, detail);

    public static IActionResult PreconditionFailed(HttpContext context, string slug, string? detail = null)
        => Create(context, StatusCodes.Status412PreconditionFailed, "Precondition Failed", slug, detail);

    public static IActionResult PayloadTooLarge(HttpContext context, string slug, string? detail = null)
        => Create(context, StatusCodes.Status413PayloadTooLarge, "Payload Too Large", slug, detail);

    /// <summary>
    /// RFC 6585 §3 — the origin server requires the request to be conditional.
    /// Returned when a mutating request omits an expected <c>If-Match</c>
    /// header on an endpoint that uses ETag-based optimistic concurrency.
    /// </summary>
    public static IActionResult PreconditionRequired(HttpContext context, string slug, string? detail = null)
        => Create(context, StatusCodes.Status428PreconditionRequired, "Precondition Required", slug, detail);

    /// <summary>
    /// 429 response with RFC 9110 <c>Retry-After</c> header when a duration
    /// is supplied. Callers that have a richer hint (e.g. shared rate-limiter
    /// pause budget) should pass <paramref name="retryAfterSeconds"/> so the
    /// client can schedule instead of guessing.
    /// </summary>
    public static IActionResult TooManyRequests(HttpContext context, string slug, string? detail = null, int? retryAfterSeconds = null)
    {
        if (retryAfterSeconds is int seconds && seconds >= 0)
        {
            context.Response.Headers.Append("Retry-After", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return Create(context, StatusCodes.Status429TooManyRequests, "Too Many Requests", slug, detail);
    }

    public static IActionResult UpstreamFailed(HttpContext context, string slug, string? detail = null)
        => Create(context, StatusCodes.Status502BadGateway, "Bad Gateway", slug, detail);

    public static IActionResult ServiceUnavailable(HttpContext context, string slug, string? detail = null)
        => Create(context, StatusCodes.Status503ServiceUnavailable, "Service Unavailable", slug, detail);

    public static IActionResult InternalError(HttpContext context, string slug, string? detail = null)
        => Create(context, StatusCodes.Status500InternalServerError, "Internal Server Error", slug, detail);

    private static IActionResult Create(
        HttpContext context,
        int statusCode,
        string title,
        string slug,
        string? detail,
        IDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails
        {
            Type = $"{TypeBase}#{slug}",
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path.Value,
        };

        var traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrEmpty(traceId))
        {
            problem.Extensions["traceId"] = traceId;
        }

        if (extensions is not null)
        {
            foreach (var kvp in extensions)
            {
                problem.Extensions[kvp.Key] = kvp.Value;
            }
        }

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { ContentType },
        };
    }
}
