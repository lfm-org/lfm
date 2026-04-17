// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Instances;

namespace Lfm.App.Services;

public interface IInstancesClient
{
    Task<IReadOnlyList<InstanceDto>> ListAsync(CancellationToken ct);
}
