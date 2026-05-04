// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Lfm.Api.Helpers;
using Lfm.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Lfm.Api.Functions;

/// <summary>
/// Anonymous browser diagnostics endpoint for Core Web Vitals RUM. The payload
/// is deliberately narrow and non-identifying: no user ids, Battle.net ids,
/// query strings, fragments, or raw URLs are accepted into telemetry.
/// </summary>
public sealed class WebVitalsFunction(IWebVitalsTelemetry telemetry)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedConnectionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "slow-2g", "2g", "3g", "4g", "unknown"
    };

    private static readonly Dictionary<string, MetricPolicy> MetricPolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lcp"] = new("webvital_lcp", 600_000),
        ["inp"] = new("webvital_inp", 600_000),
        ["cls"] = new("webvital_cls", 100),
        ["fcp"] = new("webvital_fcp", 600_000),
        ["ttfb"] = new("webvital_ttfb", 600_000),
    };

    [Function("diagnostics-web-vitals")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/diagnostics/web-vitals")] HttpRequest req,
        CancellationToken ct)
    {
        WebVitalsRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<WebVitalsRequest>(req.Body, JsonOptions, ct);
        }
        catch (JsonException)
        {
            return Problem.BadRequest(req.HttpContext, "invalid-body", "Request body is invalid or missing.");
        }

        if (body is null)
            return Problem.BadRequest(req.HttpContext, "invalid-body", "Request body is invalid or missing.");

        var validation = BuildMetric(body);
        if (validation.Error is { } error)
            return Problem.BadRequest(req.HttpContext, error.Code, error.Message);

        telemetry.Track(validation.Metric!);
        return new StatusCodeResult(StatusCodes.Status202Accepted);
    }

    private static MetricValidation BuildMetric(WebVitalsRequest body)
    {
        var name = body.Name?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || !MetricPolicies.TryGetValue(name, out var policy))
            return Error("invalid-web-vital-metric", "Web vital metric name is not supported.");

        if (!body.Value.HasValue || !double.IsFinite(body.Value.Value) || body.Value.Value < 0 || body.Value.Value > policy.MaxValue)
            return Error("invalid-web-vital-value", "Web vital metric value is outside the accepted range.");

        if (IsTooLong(body.Id, 128) || IsTooLong(body.NavigationType, 32) || IsTooLong(body.EffectiveConnectionType, 16))
            return Error("invalid-web-vital-field", "Web vital metadata field is too long.");

        if (body.Viewport is { } viewport
            && (!IsViewportDimension(viewport.Width) || !IsViewportDimension(viewport.Height)))
        {
            return Error("invalid-web-vital-viewport", "Web vital viewport dimensions are outside the accepted range.");
        }

        var connection = string.IsNullOrWhiteSpace(body.EffectiveConnectionType)
            ? "unknown"
            : body.EffectiveConnectionType.Trim().ToLowerInvariant();
        if (!AllowedConnectionTypes.Contains(connection))
            connection = "unknown";

        var path = SanitizePath(body.Path);
        var metric = new WebVitalsMetric(
            MetricName: policy.TelemetryName,
            WebVitalName: name,
            Value: body.Value.Value,
            Id: Truncate(body.Id, 128),
            NavigationType: Truncate(body.NavigationType, 32),
            Path: path,
            ViewportWidth: body.Viewport?.Width,
            ViewportHeight: body.Viewport?.Height,
            EffectiveConnectionType: connection,
            ClientTimestamp: body.Timestamp);

        return new MetricValidation(metric, null);
    }

    private static string SanitizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        var trimmed = path.Trim();
        var queryIndex = trimmed.IndexOfAny(['?', '#']);
        if (queryIndex >= 0)
            trimmed = trimmed[..queryIndex];

        if (!trimmed.StartsWith('/'))
            trimmed = "/";

        return Truncate(trimmed, 256);
    }

    private static bool IsViewportDimension(int? value) => value is >= 1 and <= 10_000;

    private static bool IsTooLong(string? value, int maxLength) => value?.Length > maxLength;

    private static string Truncate(string? value, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static MetricValidation Error(string code, string message) =>
        new(null, new ValidationError(code, message));

    private sealed record MetricPolicy(string TelemetryName, double MaxValue);
    private sealed record MetricValidation(WebVitalsMetric? Metric, ValidationError? Error);
    private sealed record ValidationError(string Code, string Message);
}

public sealed record WebVitalsRequest(
    string? Name,
    double? Value,
    string? Id,
    string? NavigationType,
    string? Path,
    WebVitalsViewport? Viewport,
    string? EffectiveConnectionType,
    DateTimeOffset? Timestamp);

public sealed record WebVitalsViewport(int? Width, int? Height);
