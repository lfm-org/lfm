// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Instances;

/// <param name="Id">
/// Composite dropdown-option key of the form <c>"{instanceId}:{modeKey}"</c>.
/// Used as the selected-option value in Razor. Do not parse as an int —
/// use <paramref name="InstanceNumericId"/> for that.
/// </param>
/// <param name="InstanceNumericId">
/// Numeric Blizzard journal-instance id. Used wherever the wire / Cosmos
/// schema wants an <c>int</c> (e.g. <c>CreateRunRequest.InstanceId</c>).
/// </param>
/// <param name="ModeKey">
/// Legacy composite mode key (<c>"{Difficulty}:{Size}"</c>) retained for
/// cross-compatibility during the create-run page reshape — remove once all
/// consumers switch to <paramref name="Difficulty"/> + <paramref name="Size"/>.
/// </param>
/// <param name="Category">
/// Blizzard journal-instance category type — <c>"RAID"</c> or <c>"DUNGEON"</c>.
/// Null on legacy manifests that predate PR 3 of the staged create-run page
/// refactor; a manual reference-refresh repopulates. Planned consumer: the
/// activity segmented control on the create-run form (tracked under the
/// wire-payload-contract "planned near-term feature reservation" exception —
/// see docs/wire-payload-contract.md).
/// </param>
/// <param name="ExpansionId">
/// Blizzard expansion id for this instance. Null on legacy manifests; a
/// manual reference-refresh repopulates. Planned consumer: the expansion
/// selector on the create-run form (same reservation exception as
/// <paramref name="Category"/>).
/// </param>
/// <param name="Difficulty">
/// Blizzard mode-type enum — <c>"NORMAL"</c>, <c>"HEROIC"</c>, <c>"MYTHIC"</c>,
/// <c>"LFR"</c>, <c>"MYTHIC_KEYSTONE"</c>, etc. Empty string on legacy
/// manifests predating PR 3; a manual reference-refresh repopulates. Planned
/// consumer: the difficulty segmented control on the create-run form (same
/// reservation exception as <paramref name="Category"/>).
/// </param>
/// <param name="Size">
/// Player count Blizzard returns for this mode. 0 on legacy manifests
/// predating PR 3; a manual reference-refresh repopulates. Planned consumer:
/// the difficulty display label on the create-run form (same reservation
/// exception as <paramref name="Category"/>).
/// </param>
/// <param name="PortraitUrl">
/// Reserved for the in-flight instance-portrait UI. No app consumer today.
/// Tracked by the wire-payload-contract "planned near-term feature reservation"
/// exception — see docs/wire-payload-contract.md. If the portrait surface is
/// dropped, trim this field at the next audit.
/// </param>
public sealed record InstanceDto(
    string Id,
    int InstanceNumericId,
    string Name,
    string ModeKey,
    string Expansion,
    string? Category = null,
    int? ExpansionId = null,
    string Difficulty = "",
    int Size = 0,
    string? PortraitUrl = null);
