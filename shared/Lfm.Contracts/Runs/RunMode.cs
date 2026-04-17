// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Runs;

/// <summary>
/// Parses run/instance mode keys of the form "DIFFICULTY:SIZE" (e.g. "NORMAL:25", "HEROIC:5").
/// </summary>
public static class RunMode
{
    public static (string Difficulty, int Size) Parse(string? modeKey)
    {
        if (string.IsNullOrWhiteSpace(modeKey))
        {
            return ("", 0);
        }

        var colon = modeKey.IndexOf(':');
        if (colon < 0)
        {
            return (modeKey.ToUpperInvariant(), 0);
        }

        var difficulty = modeKey[..colon].ToUpperInvariant();
        var sizeSpan = modeKey[(colon + 1)..];
        _ = int.TryParse(sizeSpan, out var size);
        return (difficulty, size);
    }
}
