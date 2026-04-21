// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Runs;

/// <summary>
/// Sanitized run document returned by GET /api/runs.
/// Wire-only shape per docs/wire-payload-contract.md — omits storage-internal
/// fields (Ttl, CreatedAt) and audit identifiers the app does not render
/// (CreatorGuildId, CreatorBattleNetId).
/// </summary>
public sealed record RunSummaryDto(
    string Id,
    string StartTime,
    string SignupCloseTime,
    string Description,
    string ModeKey,
    string Visibility,
    string CreatorGuild,
    int InstanceId,
    string InstanceName,
    IReadOnlyList<RunCharacterDto> RunCharacters);
