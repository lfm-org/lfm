// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Instances;

/// <param name="PortraitUrl">
/// Reserved for the in-flight instance-portrait UI. No app consumer today.
/// Tracked by the wire-payload-contract "planned near-term feature reservation"
/// exception — see docs/wire-payload-contract.md. If the portrait surface is
/// dropped, trim this field at the next audit.
/// </param>
public sealed record InstanceDto(
    string Id,
    string Name,
    string ModeKey,
    string Expansion,
    string? PortraitUrl = null);
