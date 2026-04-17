// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Runs;

/// <summary>
/// Sanitized run document returned by GET /api/runs/{id}.
/// Contains the same fields as RunSummaryDto; defined as a distinct type so
/// that detail-specific fields can be added later without breaking the list
/// endpoint contract.
/// Mirrors RunDocumentResponse in functions/src/lib/runResponseSanitizer.ts.
/// </summary>
public sealed record RunDetailDto(
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
