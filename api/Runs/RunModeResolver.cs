// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Runs;

/// <summary>
/// Resolves the canonical typed mode fields (<c>Difficulty</c>, <c>Size</c>) for
/// a run, preferring the explicitly-set typed fields and falling back to
/// parsing the legacy composite <c>ModeKey</c> when they are empty. Used by
/// every handler that projects a <c>RunDocument</c> onto a wire DTO so
/// consumers always see populated structured fields — even on Cosmos documents
/// that predate the PR 5 schema migration.
/// </summary>
internal static class RunModeResolver
{
    internal static (string Difficulty, int Size) Resolve(string? difficulty, int size, string? modeKey)
    {
        if (!string.IsNullOrEmpty(difficulty) && size > 0)
            return (difficulty, size);

        var parsed = RunMode.Parse(modeKey);
        return (
            !string.IsNullOrEmpty(difficulty) ? difficulty : parsed.Difficulty,
            size > 0 ? size : parsed.Size);
    }

    /// <summary>
    /// Ensures a persisted <see cref="RunDocument"/> has the typed mode
    /// fields populated, deriving them from <see cref="RunDocument.ModeKey"/>
    /// when they are missing (legacy documents). Returns the same instance
    /// when nothing needs to change.
    /// </summary>
    internal static RunDocument EnsurePopulated(RunDocument doc)
    {
        if (!string.IsNullOrEmpty(doc.Difficulty) && doc.Size > 0)
            return doc;

        var (difficulty, size) = Resolve(doc.Difficulty, doc.Size, doc.ModeKey);
        return doc with { Difficulty = difficulty, Size = size };
    }
}
