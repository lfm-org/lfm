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
    string? PortraitUrl = null);
