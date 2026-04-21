// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Runs;

/// <summary>
/// Sanitized representation of a run participant.
/// Wire-only shape per docs/wire-payload-contract.md — omits raiderBattleNetId
/// (PII) and adds IsCurrentUser. Fields the app does not render
/// (Id, CharacterId, CharacterLevel, CharacterRaceId/Name, SpecId) are omitted.
/// </summary>
public sealed record RunCharacterDto(
    string CharacterName,
    string CharacterRealm,
    int CharacterClassId,
    string CharacterClassName,
    string DesiredAttendance,
    string ReviewedAttendance,
    string? SpecName,
    string? Role,
    bool IsCurrentUser);
