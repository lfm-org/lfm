// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.Runs;

/// <summary>
/// Thrown by <see cref="Services.IRunsClient.UpdateAsync"/> when the server
/// rejects a PUT with 412 Precondition Failed — the <c>If-Match</c> ETag
/// the page loaded is stale and another client has modified the run since.
/// Pages should catch this distinctly and prompt the user to reload.
/// </summary>
public sealed class StaleEtagException : Exception
{
    public StaleEtagException(string? detail = null)
        : base(detail ?? "Run modified since loaded.") { }
}
