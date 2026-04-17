// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Runs;

/// <summary>
/// Sanitized run document returned to API callers.
/// Mirrors RunDocumentResponse in functions/src/lib/runResponseSanitizer.ts:
///   - runCharacters contain RunCharacterDto (raiderBattleNetId stripped, isCurrentUser added)
///   - all other fields pass through unchanged
/// </summary>
public sealed record RunSummaryDto(
    string Id,
    string StartTime,
    string SignupCloseTime,
    string Description,
    string ModeKey,
    string Visibility,
    string CreatorGuild,
    int? CreatorGuildId,
    int InstanceId,
    string InstanceName,
    string? CreatorBattleNetId,
    string CreatedAt,
    int Ttl,
    IReadOnlyList<RunCharacterDto> RunCharacters);
