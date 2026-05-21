// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Services;

public interface IWowMediaCache
{
    Task<ReferenceBlobContent?> GetOrFetchAsync(string encodedSource, CancellationToken ct);
}
