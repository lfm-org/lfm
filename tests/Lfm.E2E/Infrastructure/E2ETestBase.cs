using System.Collections.Concurrent;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Lfm.E2E.Infrastructure;

/// <summary>
/// Abstract base class for all E2E spec classes. Provides structured output
/// formatting, failure diagnostics (screenshots, traces, DOM snapshots),
/// browser console/error logging, and request-failure logging.
///
/// Subclasses must set <see cref="Page"/> and <see cref="Context"/> before
/// tests execute (typically in <see cref="InitializeAsync"/>).
///
/// Diagnostics (screenshots, traces, DOM snapshots) are always captured in
/// DisposeAsync. This ensures failure artifacts are available regardless of
/// how the test fails. The overhead is minimal and artifacts are gitignored.
/// </summary>
public abstract class E2ETestBase : IAsyncLifetime
{
    private const string ArtifactsRoot = "artifacts/e2e-results";
    private const string ScreenshotsDir = $"{ArtifactsRoot}/screenshots";
    private const string TracesDir = $"{ArtifactsRoot}/traces";
    private const string DomSnapshotsDir = $"{ArtifactsRoot}/dom-snapshots";

    private readonly ITestOutputHelper _output;
    private readonly ConcurrentQueue<string> _consoleMessages = new();
    private readonly ConcurrentQueue<string> _requestFailures = new();

    protected E2ETestBase(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Playwright page used by the current test. Set by subclasses.</summary>
    protected IPage? Page { get; set; }

    /// <summary>Browser context used by the current test. Set by subclasses.</summary>
    protected IBrowserContext? Context { get; set; }

    /// <summary>Exposes the output helper for use by static helpers.</summary>
    protected ITestOutputHelper Output => _output;

    /// <summary>
    /// Name used for artifact file names. Defaults to the runtime type name.
    /// Subclasses may override for more descriptive names.
    /// </summary>
    protected virtual string TestName => GetType().Name;

    public virtual async Task InitializeAsync()
    {
        if (Context is not null)
        {
            await Context.Tracing.StartAsync(new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
            });
        }
    }

    public virtual async Task DisposeAsync()
    {
        if (Page is not null)
        {
            await CaptureFailureDiagnosticsAsync();
        }

        LogCapturedMessages();
    }

    /// <summary>
    /// Call this at the start of each test method to wire up console and
    /// request-failure listeners on the page.
    /// </summary>
    protected void AttachDiagnosticListeners()
    {
        if (Page is null) return;

        Page.Console += (_, msg) =>
        {
            if (msg.Type is "error" or "warning")
            {
                var line = $"[Browser {msg.Type.ToUpper()}] {msg.Text}";
                _consoleMessages.Enqueue(line);
            }
        };

        Page.RequestFailed += (_, req) =>
        {
            var line = $"[Browser REQUESTFAILED] {req.Url} - {req.Failure}";
            _requestFailures.Enqueue(line);
        };
    }

    /// <summary>
    /// Writes a PASS line to structured output.
    /// </summary>
    protected void LogPass(string testName, TimeSpan duration)
    {
        _output.WriteLine($"[PASS] {testName} ({duration.TotalSeconds:F1}s)");
    }

    /// <summary>
    /// Writes a FAIL line to structured output.
    /// </summary>
    protected void LogFail(string testName, TimeSpan duration, string? error = null)
    {
        _output.WriteLine($"[FAIL] {testName} ({duration.TotalSeconds:F1}s)");
        if (error is not null)
        {
            _output.WriteLine($"  {error}");
        }
    }

    /// <summary>
    /// Writes an informational line to the test output.
    /// </summary>
    protected void Log(string message) => _output.WriteLine(message);

    private async Task CaptureFailureDiagnosticsAsync()
    {
        if (Page is null) return;

        var repoRoot = FindRepoRoot();
        await CaptureScreenshotAsync(repoRoot);
        await CaptureTraceAsync(repoRoot);
        await CaptureDomSnapshotAsync(repoRoot);
    }

    private async Task CaptureScreenshotAsync(string repoRoot)
    {
        try
        {
            var dir = Path.Combine(repoRoot, ScreenshotsDir);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{TestName}.png");
            await Page!.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = path,
                FullPage = true,
            });
            _output.WriteLine($"[ARTIFACT] Screenshot: {path}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[WARN] Failed to capture screenshot: {ex.Message}");
        }
    }

    private async Task CaptureTraceAsync(string repoRoot)
    {
        if (Context is null) return;

        try
        {
            var dir = Path.Combine(repoRoot, TracesDir);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{TestName}.zip");
            await Context.Tracing.StopAsync(new TracingStopOptions { Path = path });
            _output.WriteLine($"[ARTIFACT] Trace: {path}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[WARN] Failed to capture trace: {ex.Message}");
        }
    }

    private async Task CaptureDomSnapshotAsync(string repoRoot)
    {
        try
        {
            var dir = Path.Combine(repoRoot, DomSnapshotsDir);
            Directory.CreateDirectory(dir);
            var html = await Page!.ContentAsync();
            var path = Path.Combine(dir, $"{TestName}.html");
            await File.WriteAllTextAsync(path, html);
            _output.WriteLine($"[ARTIFACT] DOM snapshot: {path}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[WARN] Failed to capture DOM snapshot: {ex.Message}");
        }
    }

    private void LogCapturedMessages()
    {
        while (_consoleMessages.TryDequeue(out var msg))
        {
            _output.WriteLine(msg);
        }

        while (_requestFailures.TryDequeue(out var msg))
        {
            _output.WriteLine(msg);
        }
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
}
