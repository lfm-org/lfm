// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Expansions;

namespace Lfm.App.Services;

public interface IExpansionsClient
{
    Task<IReadOnlyList<ExpansionDto>> ListAsync(CancellationToken ct);
}
