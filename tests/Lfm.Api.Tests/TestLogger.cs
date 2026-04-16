// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Extensions.Logging;

namespace Lfm.Api.Tests;

/// <summary>
/// Deterministic capture-only ILogger used by unit tests. Records every
/// invocation into <see cref="Entries"/> along with the named placeholders
/// that were passed as structured state, so assertions can match on
/// properties (e.g. "AuditAction") instead of substring-matching the
/// rendered message. Prefer property-based assertions over message matching.
/// </summary>
public sealed class TestLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = new();

    IDisposable? ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (state is IReadOnlyList<KeyValuePair<string, object?>> kvps)
        {
            foreach (var kv in kvps)
                properties[kv.Key] = kv.Value;
        }

        Entries.Add(new LogEntry(
            Level: logLevel,
            EventId: eventId,
            Message: formatter(state, exception),
            Exception: exception,
            Properties: properties));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

public sealed record LogEntry(
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties)
{
    /// <summary>
    /// True when this entry's properties match the given AuditLog.Emit fields.
    /// Pass null for fields you don't care about.
    /// </summary>
    public bool IsAudit(string? action = null, string? actorId = null, string? result = null, string? targetId = null)
    {
        if (action is not null && GetProp("AuditAction") != action) return false;
        if (actorId is not null && GetProp("AuditActorId") != actorId) return false;
        if (result is not null && GetProp("AuditResult") != result) return false;
        if (targetId is not null && GetProp("AuditTargetId") != targetId) return false;
        return true;
    }

    /// <summary>
    /// Shortcut for asserting action + result + detail on a failure-path audit event.
    /// Avoids the awkward two-step ContainSingle(...).Subject.Properties[Detail] chain.
    /// </summary>
    public bool IsAudit(string action, string result, string detail) =>
        IsAudit(action: action, actorId: null, result: result, targetId: null)
        && GetProp(AuditProperties.Detail) == detail;

    private string? GetProp(string name) =>
        Properties.TryGetValue(name, out var value) ? value?.ToString() : null;
}

/// <summary>
/// Canonical names of the structured log properties emitted by
/// <c>AuditLog.Emit</c>. Use these constants when reading
/// <see cref="LogEntry.Properties"/> so typos become compile errors:
/// <c>entry.Properties[AuditProperties.Action]</c>.
/// </summary>
public static class AuditProperties
{
    public const string Action = "AuditAction";
    public const string ActorId = "AuditActorId";
    public const string TargetId = "AuditTargetId";
    public const string Result = "AuditResult";
    public const string Detail = "AuditDetail";
}
