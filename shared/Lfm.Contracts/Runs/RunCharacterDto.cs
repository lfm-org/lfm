// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Runs;

/// <summary>
/// Sanitized representation of a run participant.
/// Wire-only shape per docs/wire-payload-contract.md — omits raiderBattleNetId
/// (PII), the internal run-character id, level, and race fields, and adds
/// IsCurrentUser.
/// </summary>
public sealed record RunCharacterDto(
    string CharacterId,
    string CharacterName,
    string CharacterRealm,
    int CharacterClassId,
    string CharacterClassName,
    string DesiredAttendance,
    string ReviewedAttendance,
    int? SpecId,
    string? SpecName,
    string? Role,
    bool IsCurrentUser);
