// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Lfm.E2E.Infrastructure;
using Lfm.E2E.Seeds;
using Xunit;
using Xunit.Abstractions;

namespace Lfm.E2E.Specs;

[Collection("Runs")]
[Trait("Category", E2ELanes.PerformanceLoad)]
public sealed class PerformanceLoadSpec(RunsFixture fixture, ITestOutputHelper output)
{
    private const int ReportSchemaVersion = 2;
    private const int Concurrency = 2;
    private const int RequestsPerProbe = 6;
    private const int AllowedFailureThreshold = 0;

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [Fact]
    [Trait("Category", E2ELanes.PerformanceLoad)]
    public async Task Local_stack_smoke_load_probe_reports_request_health()
    {
        using var anonymousHandler = new HttpClientHandler();
        using var anonymousClient = new HttpClient(anonymousHandler);
        using var authenticatedHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = new CookieContainer(),
            UseCookies = true,
        };
        using var authenticatedClient = new HttpClient(authenticatedHandler);

        await AuthenticateAsync(authenticatedClient, authenticatedHandler.CookieContainer);

        var appBaseUri = new Uri(fixture.Stack.AppBaseUrl);
        var apiBaseUri = new Uri(fixture.Stack.ApiBaseUrl);
        var probes = new[]
        {
            new LoadProbe(
                "public-landing",
                "frontend",
                "anonymous public landing static host",
                anonymousClient,
                HttpMethod.Get,
                new Uri(appBaseUri, "/"),
                [200]),
            new LoadProbe(
                "api-live-health",
                "api-health",
                "anonymous API live health",
                anonymousClient,
                HttpMethod.Get,
                new Uri(apiBaseUri, "/api/health"),
                [200]),
            new LoadProbe(
                "api-ready-health",
                "api-health",
                "anonymous API readiness",
                anonymousClient,
                HttpMethod.Get,
                new Uri(apiBaseUri, "/api/health/ready"),
                [200]),
            new LoadProbe(
                "authenticated-runs-api",
                "authenticated-api",
                "authenticated runs list",
                authenticatedClient,
                HttpMethod.Get,
                new Uri(apiBaseUri, "/api/v1/runs"),
                [200]),
            new LoadProbe(
                "run-create-guild-dependency",
                "authenticated-api",
                "authenticated create-run guild dependency",
                authenticatedClient,
                HttpMethod.Get,
                new Uri(apiBaseUri, "/api/v1/guild"),
                [200]),
            new LoadProbe(
                "run-create-expansions-dependency",
                "authenticated-reference-api",
                "authenticated create-run expansions dependency",
                authenticatedClient,
                HttpMethod.Get,
                new Uri(apiBaseUri, "/api/v1/wow/reference/expansions"),
                [200]),
            new LoadProbe(
                "run-create-instances-dependency",
                "authenticated-reference-api",
                "authenticated create-run instances dependency",
                authenticatedClient,
                HttpMethod.Get,
                new Uri(apiBaseUri, "/api/v1/wow/reference/instances"),
                [200]),
            new LoadProbe(
                "characters-list-api",
                "authenticated-api",
                "authenticated cached characters list",
                authenticatedClient,
                HttpMethod.Get,
                new Uri(apiBaseUri, "/api/v1/battlenet/characters"),
                [200]),
            new LoadProbe(
                "signup-options-api",
                "authenticated-api",
                "authenticated seeded run signup options",
                authenticatedClient,
                HttpMethod.Get,
                new Uri(apiBaseUri, $"/api/v1/runs/{DefaultSeed.TestRunId}/signup/options"),
                [200, 204]),
        };

        var probeReports = new List<ProbeReport>();
        foreach (var probe in probes)
        {
            var report = await RunProbeAsync(probe);
            probeReports.Add(report);
            output.WriteLine(
                $"[PERFLOAD] {report.Name}: requests={report.RequestCount}, failures={report.FailureCount}, " +
                $"p50={report.P50Ms}ms, p75={report.P75Ms}ms, p95={report.P95Ms}ms, max={report.MaxMs}ms");
        }

        var loadReport = new PerformanceLoadReport(
            ReportSchemaVersion,
            DateTimeOffset.UtcNow,
            new LoadRunMetadata(
                "local-stack",
                Concurrency,
                RequestsPerProbe,
                RequestsPerProbe * probes.Length,
                (long)ProbeTimeout.TotalMilliseconds,
                (long)RequestTimeout.TotalMilliseconds,
                AllowedFailureThreshold,
                "Fails on request errors/status mismatches only; timing percentiles are report evidence, not budgets."),
            probeReports);

        var reportPath = WriteReport(loadReport);
        output.WriteLine($"[ARTIFACT] Performance load report: {reportPath}");

        var failures = probeReports
            .Where(report => report.FailureCount > AllowedFailureThreshold)
            .Select(report =>
                $"{report.Name} had {report.FailureCount} failures over explicit threshold " +
                $"{AllowedFailureThreshold}: " +
                string.Join("; ", report.Samples.Where(sample => !sample.Success)
                    .Select(sample => sample.Error ?? $"{sample.StatusCode} {sample.StatusDescription}")))
            .ToArray();

        Assert.True(
            failures.Length == 0,
            "Performance load smoke probe exceeded request-failure threshold:\n" +
            string.Join("\n", failures));
    }

    private async Task AuthenticateAsync(HttpClient client, CookieContainer cookies)
    {
        var apiBaseUri = new Uri(fixture.Stack.ApiBaseUrl);
        var loginUri = new Uri(
            apiBaseUri,
            "/api/e2e/login"
            + $"?battleNetId={Uri.EscapeDataString(DefaultSeed.PrimaryBattleNetId)}"
            + $"&redirect={Uri.EscapeDataString("/runs")}");

        using var response = await client.GetAsync(loginUri);
        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.RedirectKeepVerb,
            $"Expected E2E login redirect but got {(int)response.StatusCode} {response.ReasonPhrase}");

        var authCookies = cookies.GetCookies(apiBaseUri);
        Assert.Contains(authCookies.Cast<Cookie>(), cookie => cookie.Name == "battlenet_token");
    }

    private static async Task<ProbeReport> RunProbeAsync(LoadProbe probe)
    {
        using var probeTimeout = new CancellationTokenSource(ProbeTimeout);
        using var gate = new SemaphoreSlim(Concurrency, Concurrency);

        var tasks = Enumerable.Range(1, RequestsPerProbe)
            .Select(index => RunSampleAsync(probe, index, gate, probeTimeout.Token))
            .ToArray();

        var samples = await Task.WhenAll(tasks);
        var elapsed = samples.Select(sample => sample.ElapsedMs).ToArray();

        return new ProbeReport(
            probe.Name,
            probe.Group,
            probe.Journey,
            probe.Method.Method,
            probe.Url.AbsolutePath,
            probe.Url.GetLeftPart(UriPartial.Authority),
            probe.ExpectedStatusCodes,
            samples.Length,
            samples.Count(sample => !sample.Success),
            PerformanceMetricsHelper.Percentile(elapsed, 50),
            PerformanceMetricsHelper.Percentile(elapsed, 75),
            PerformanceMetricsHelper.Percentile(elapsed, 95),
            elapsed.Max(),
            samples);
    }

    private static async Task<LoadSample> RunSampleAsync(
        LoadProbe probe,
        int sequence,
        SemaphoreSlim gate,
        CancellationToken probeCancellationToken)
    {
        await gate.WaitAsync(probeCancellationToken);
        try
        {
            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(probeCancellationToken);
            requestTimeout.CancelAfter(RequestTimeout);

            var sw = Stopwatch.StartNew();
            try
            {
                using var request = new HttpRequestMessage(probe.Method, probe.Url);
                using var response = await probe.Client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    requestTimeout.Token);
                var body = await response.Content.ReadAsByteArrayAsync(requestTimeout.Token);
                sw.Stop();

                var statusCode = (int)response.StatusCode;
                return new LoadSample(
                    sequence,
                    (long)sw.Elapsed.TotalMilliseconds,
                    statusCode,
                    response.ReasonPhrase,
                    body.Length,
                    probe.ExpectedStatusCodes.Contains(statusCode),
                    null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                sw.Stop();
                return new LoadSample(
                    sequence,
                    (long)sw.Elapsed.TotalMilliseconds,
                    null,
                    null,
                    null,
                    false,
                    ex.Message);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static string WriteReport(PerformanceLoadReport report)
    {
        var repoRoot = FindRepoRoot();
        var outputDir = Path.Combine(repoRoot, "artifacts", "e2e-results");
        Directory.CreateDirectory(outputDir);

        var path = Path.Combine(outputDir, "performance-load-report.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
        return path;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "lfm.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Could not find lfm.sln walking up from " + AppContext.BaseDirectory);
    }

    private sealed record LoadProbe(
        string Name,
        string Group,
        string Journey,
        HttpClient Client,
        HttpMethod Method,
        Uri Url,
        int[] ExpectedStatusCodes);

    private sealed record PerformanceLoadReport(
        int SchemaVersion,
        DateTimeOffset GeneratedAt,
        LoadRunMetadata Run,
        IReadOnlyList<ProbeReport> Probes);

    private sealed record LoadRunMetadata(
        string StackTarget,
        int Concurrency,
        int RequestsPerProbe,
        int TotalRequestLimit,
        long ProbeTimeoutMs,
        long RequestTimeoutMs,
        int AllowedFailureThreshold,
        string GatePolicy);

    private sealed record ProbeReport(
        string Name,
        string Group,
        string Journey,
        string Method,
        string Endpoint,
        string Origin,
        IReadOnlyList<int> ExpectedStatusCodes,
        int RequestCount,
        int FailureCount,
        long P50Ms,
        long P75Ms,
        long P95Ms,
        long MaxMs,
        IReadOnlyList<LoadSample> Samples);

    private sealed record LoadSample(
        int Sequence,
        long ElapsedMs,
        int? StatusCode,
        string? StatusDescription,
        int? ResponseBytes,
        bool Success,
        string? Error);
}
