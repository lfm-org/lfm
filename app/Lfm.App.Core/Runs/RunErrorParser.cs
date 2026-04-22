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
    /// <summary>Field-level validation error (HTTP 400 with {errors: [...]}).</summary>
    Validation,

    /// <summary>Guild rank denied (HTTP 403 with the specific rank-denied body).</summary>
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
/// </summary>
public static class RunErrorParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

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
            // Server shape from CreateRunRequestValidator / UpdateRunRequestValidator:
            //   { errors: [ "msg1", "msg2", ... ] }
            // or a single error { error: "msg" } for guards.
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

            if (doc.RootElement.TryGetProperty("error", out var single) && single.ValueKind == JsonValueKind.String)
            {
                return new RunError(RunErrorKind.Validation, [single.GetString() ?? ""]);
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
        // The RunsCreateFunction returns:
        //   { error: "Guild run creation is not enabled for your rank" }
        // on rank-denied. Recognise that specifically so the form can surface
        // it inline under the visibility control rather than as a toast.
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err)
                    && err.ValueKind == JsonValueKind.String)
                {
                    var msg = err.GetString() ?? "";
                    if (msg.Contains("rank", StringComparison.OrdinalIgnoreCase))
                        return new RunError(RunErrorKind.GuildRankDenied, [msg]);
                    return new RunError(RunErrorKind.GuildRankDenied, [msg]);
                }
            }
            catch (JsonException) { /* fall through */ }
        }
        return new RunError(RunErrorKind.GuildRankDenied, ["Forbidden."]);
    }
}
