// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;

namespace Lfm.Api.Services;

public interface IWebVitalsTelemetry
{
    void Track(WebVitalsMetric metric);
}

public sealed record WebVitalsMetric(
    string MetricName,
    string WebVitalName,
    double Value,
    string Id,
    string NavigationType,
    string Path,
    int? ViewportWidth,
    int? ViewportHeight,
    string EffectiveConnectionType,
    DateTimeOffset? ClientTimestamp);

public sealed class ApplicationInsightsWebVitalsTelemetry(
    TelemetryClient telemetry,
    ILogger<ApplicationInsightsWebVitalsTelemetry> logger) : IWebVitalsTelemetry
{
    public void Track(WebVitalsMetric metric)
    {
        var item = new MetricTelemetry(metric.MetricName, metric.Value);
        item.Properties["webVitalName"] = metric.WebVitalName;
        item.Properties["id"] = metric.Id;
        item.Properties["navigationType"] = metric.NavigationType;
        item.Properties["path"] = metric.Path;
        item.Properties["effectiveConnectionType"] = metric.EffectiveConnectionType;
        if (metric.ViewportWidth is { } width)
            item.Properties["viewportWidth"] = width.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (metric.ViewportHeight is { } height)
            item.Properties["viewportHeight"] = height.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (metric.ClientTimestamp is { } timestamp)
            item.Properties["clientTimestamp"] = timestamp.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        telemetry.TrackMetric(item);
        logger.LogInformation(
            "Web vital {WebVitalName} value={WebVitalValue} path={WebVitalPath}",
            metric.WebVitalName,
            metric.Value,
            metric.Path);
    }
}
