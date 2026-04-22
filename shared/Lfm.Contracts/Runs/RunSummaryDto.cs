// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Runs;

/// <summary>
/// Sanitized run document returned by GET /api/runs.
/// Wire-only shape per docs/wire-payload-contract.md — omits storage-internal
/// fields (Ttl, CreatedAt) and audit identifiers the app does not render
/// (CreatorGuildId, CreatorBattleNetId).
///
/// <para>
/// <b>Mode fields.</b> <see cref="Difficulty"/>, <see cref="Size"/>, and
/// <see cref="KeystoneLevel"/> are the typed fields consumers should prefer;
/// <see cref="ModeKey"/> is the legacy composite kept for one cycle during
/// the schema migration. <see cref="InstanceId"/> and <see cref="InstanceName"/>
/// are nullable because a Mythic+ "any dungeon" session has no specific
/// instance.
/// </para>
/// </summary>
public sealed record RunSummaryDto(
    string Id,
    string StartTime,
    string SignupCloseTime,
    string Description,
    string ModeKey,
    string Visibility,
    string CreatorGuild,
    int? InstanceId,
    string? InstanceName,
    IReadOnlyList<RunCharacterDto> RunCharacters,
    string Difficulty = "",
    int Size = 0,
    int? KeystoneLevel = null);
