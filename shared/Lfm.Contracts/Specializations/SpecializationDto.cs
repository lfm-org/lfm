// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Specializations;

public sealed record SpecializationDto(int Id, string Name, int ClassId, string Role, string? IconUrl);
