// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Specializations;

namespace Lfm.App.Services;

public interface ISpecializationsClient
{
    Task<IReadOnlyList<SpecializationDto>> ListAsync(CancellationToken ct);
}
