// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Instances;

public sealed record InstanceDto(
    string Id,
    string Name,
    string ModeKey,
    string Expansion,
    string? PortraitUrl = null);
