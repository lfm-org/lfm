// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Runs;

/// <summary>
/// Sanitized representation of a run participant.
/// Mirrors RunCharacterResponse in functions/src/lib/runResponseSanitizer.ts:
///   - omits raiderBattleNetId (PII)
///   - adds IsCurrentUser flag (true when the character belongs to the requesting user)
/// </summary>
public sealed record RunCharacterDto(
    string Id,
    string CharacterId,
    string CharacterName,
    string CharacterRealm,
    int CharacterLevel,
    int CharacterClassId,
    string CharacterClassName,
    int CharacterRaceId,
    string CharacterRaceName,
    string DesiredAttendance,
    string ReviewedAttendance,
    int? SpecId,
    string? SpecName,
    string? Role,
    bool IsCurrentUser);
