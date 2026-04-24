// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Text.Json;

namespace Lfm.App.Runs;

/// <summary>
/// What flavour of error to surface on the create / edit form.
/// </summary>
public enum RunErrorKind
{
    /// <summary>Field-level validation error (HTTP 400 with problem+json).</summary>
    Validation,

    /// <summary>Guild rank denied (HTTP 403 with the guild-rank-denied problem type).</summary>
    GuildRankDenied,

    /// <summary>Any other HTTP or transport failure (5xx, network, timeout).</summary>
    Network,

    /// <summary>Unknown — response shape didn't match anything expected.</summary>
    Unknown,
}

public sealed record RunError(RunErrorKind Kind, IReadOnlyList<string> Messages)
{
    public bool IsGuildRankDenied => Kind == RunErrorKind.GuildRankDenied;
    public bool IsValidation => Kind == RunErrorKind.Validation;
    public bool IsNetwork => Kind == RunErrorKind.Network;
}

/// <summary>
/// Maps a failed <c>HttpResponseMessage</c> from the runs endpoints into
/// a classified <see cref="RunError"/> so the form can surface the error
/// next to the offending control (validation) or under the visibility row
/// (rank denied) or as a toast (network). Pure; the HTTP body is read by
/// the caller (async) and passed as a string.
///
/// The server emits RFC 9457 <c>application/problem+json</c> responses on
/// error paths; the parser reads:
///   - <c>errors</c> (top-level array of strings): validation messages
///   - <c>detail</c>: human-readable error text
///   - <c>type</c>: stable URI used for classification (rank-denied detection)
/// Type URIs are rooted at <c>https://github.com/lfm-org/lfm/errors#slug</c>.
/// </summary>
public static class RunErrorParser
{
    private const string GuildRankDeniedTypeUri = "https://github.com/lfm-org/lfm/errors#guild-rank-denied";

    public static RunError Parse(HttpStatusCode status, string? body)
    {
        return status switch
        {
            HttpStatusCode.BadRequest => ParseValidation(body),
            HttpStatusCode.Forbidden => ParseForbidden(body),
            _ when (int)status >= 500 => new RunError(RunErrorKind.Network, ["Server error. Try again."]),
            _ => new RunError(RunErrorKind.Unknown, [$"Unexpected response ({(int)status})."]),
        };
    }

    /// <summary>
    /// Classifies a transport-layer exception (no HTTP status at all).
    /// </summary>
    public static RunError Network(Exception ex) =>
        new(RunErrorKind.Network, [string.IsNullOrEmpty(ex.Message) ? "Network error." : ex.Message]);

    private static RunError ParseValidation(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return new RunError(RunErrorKind.Validation, []);

        try
        {
            // Server shape (problem+json from CreateRunRequestValidator /
            // UpdateRunRequestValidator):
            //   { type, title, status, detail, errors: [ "msg1", "msg2", ... ] }
            // or a single message:
            //   { type, title, status, detail: "msg" }
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array)
            {
                var list = errs.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString() ?? "")
                    .Where(s => s.Length > 0)
                    .ToList();
                return new RunError(RunErrorKind.Validation, list);
            }

            if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                return new RunError(RunErrorKind.Validation, [detail.GetString() ?? ""]);
            }
        }
        catch (JsonException)
        {
            // fall through to generic
        }

        return new RunError(RunErrorKind.Validation, [body]);
    }

    private static RunError ParseForbidden(string? body)
    {
        // The server returns problem+json with type = GuildRankDeniedTypeUri
        // and a human-readable detail message when a GUILD run is refused due
        // to the caller's rank permission. Recognise that specifically so the
        // form surfaces it inline under the visibility control rather than as
        // a toast.
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var type = doc.RootElement.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString()
                    : null;
                var detail = doc.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String
                    ? d.GetString()
                    : null;

                if (string.Equals(type, GuildRankDeniedTypeUri, StringComparison.Ordinal))
                {
                    return new RunError(
                        RunErrorKind.GuildRankDenied,
                        [detail ?? "Guild run creation is not enabled for your rank."]);
                }

                // Problem body without the expected type URI — still treat as
                // rank-denied since the HTTP status matched ParseForbidden, but
                // prefer the server-supplied detail when present.
                if (!string.IsNullOrEmpty(detail))
                    return new RunError(RunErrorKind.GuildRankDenied, [detail!]);
            }
            catch (JsonException) { /* fall through */ }
        }
        return new RunError(RunErrorKind.GuildRankDenied, ["Forbidden."]);
    }
}
