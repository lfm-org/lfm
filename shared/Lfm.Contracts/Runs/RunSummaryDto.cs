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
/// <see cref="KeystoneLevel"/> are the canonical typed fields. The legacy
/// <c>ModeKey</c> composite was dropped from the wire — the server's
/// <c>RunModeResolver</c> still resolves it internally for Cosmos documents
/// predating the typed-fields migration, but clients only see the typed
/// fields. <see cref="InstanceId"/> and <see cref="InstanceName"/> are
/// nullable because a Mythic+ "any dungeon" session has no specific instance.
/// </para>
/// </summary>
public sealed record RunSummaryDto(
    string Id,
    string StartTime,
    string SignupCloseTime,
    string Description,
    string Visibility,
    string CreatorGuild,
    int? InstanceId,
    string? InstanceName,
    IReadOnlyList<RunCharacterDto> RunCharacters,
    string Difficulty,
    int Size,
    int? KeystoneLevel = null);
