// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text;
using System.Text.Json;
using Lfm.Api.Functions;
using Lfm.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Lfm.Api.Tests;

public class WebVitalsFunctionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class CapturingTelemetry : IWebVitalsTelemetry
    {
        public List<WebVitalsMetric> Metrics { get; } = [];
        public void Track(WebVitalsMetric metric) => Metrics.Add(metric);
    }

    private static (WebVitalsFunction Function, CapturingTelemetry Telemetry) MakeFunction()
    {
        var telemetry = new CapturingTelemetry();
        return (new WebVitalsFunction(telemetry), telemetry);
    }

    private static HttpRequest MakeRequest(object body)
        => MakeRequest(JsonSerializer.Serialize(body, JsonOptions));

    private static HttpRequest MakeRequest(string rawJson)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.ContentType = "application/json";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawJson));
        return ctx.Request;
    }

    [Theory]
    [InlineData("lcp", "webvital_lcp")]
    [InlineData("inp", "webvital_inp")]
    [InlineData("cls", "webvital_cls")]
    public async Task Run_accepts_core_web_vitals_and_tracks_metric_name(string name, string metricName)
    {
        var (fn, telemetry) = MakeFunction();
        var value = name == "cls" ? 0.123 : 123.4;

        var result = await fn.Run(MakeRequest(new
        {
            name,
            value,
            id = "vital-id",
            navigationType = "navigate",
            path = "/runs",
            viewport = new { width = 390, height = 844 },
            effectiveConnectionType = "4g",
            timestamp = DateTimeOffset.Parse("2026-05-02T22:00:00Z", System.Globalization.CultureInfo.InvariantCulture)
        }), CancellationToken.None);

        var accepted = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
        var metric = Assert.Single(telemetry.Metrics);
        Assert.Equal(metricName, metric.MetricName);
        Assert.Equal(name, metric.WebVitalName);
        Assert.Equal(value, metric.Value);
        Assert.Equal("/runs", metric.Path);
        Assert.Equal(390, metric.ViewportWidth);
        Assert.Equal(844, metric.ViewportHeight);
        Assert.Equal("4g", metric.EffectiveConnectionType);
    }

    [Fact]
    public async Task Run_strips_query_string_and_fragment_before_tracking_path()
    {
        var (fn, telemetry) = MakeFunction();

        var result = await fn.Run(MakeRequest(new
        {
            name = "lcp",
            value = 10,
            id = "id",
            navigationType = "navigate",
            path = "/runs?battleNetId=secret#section",
        }), CancellationToken.None);

        Assert.IsType<StatusCodeResult>(result);
        var metric = Assert.Single(telemetry.Metrics);
        Assert.Equal("/runs", metric.Path);
        Assert.DoesNotContain("secret", metric.Path);
        Assert.DoesNotContain("?", metric.Path);
        Assert.DoesNotContain("#", metric.Path);
    }

    [Theory]
    [InlineData("unknown", 10, "invalid-web-vital-metric")]
    [InlineData("lcp", -1, "invalid-web-vital-value")]
    [InlineData("cls", 101, "invalid-web-vital-value")]
    public async Task Run_rejects_invalid_metric_payloads(string name, double value, string errorCode)
    {
        var (fn, telemetry) = MakeFunction();

        var result = await fn.Run(MakeRequest(new { name, value, id = "id", path = "/" }), CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal($"https://github.com/lfm-org/lfm/errors#{errorCode}", problem.Type);
        Assert.Empty(telemetry.Metrics);
    }

    [Fact]
    public async Task Run_rejects_oversized_metadata_fields()
    {
        var (fn, telemetry) = MakeFunction();

        var result = await fn.Run(MakeRequest(new
        {
            name = "lcp",
            value = 10,
            id = new string('x', 129),
            path = "/"
        }), CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Empty(telemetry.Metrics);
    }

    [Fact]
    public async Task Run_rejects_invalid_json_without_tracking()
    {
        var (fn, telemetry) = MakeFunction();

        var result = await fn.Run(MakeRequest("{not json"), CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Empty(telemetry.Metrics);
    }
}
