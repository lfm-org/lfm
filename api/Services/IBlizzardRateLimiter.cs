// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Services;

public readonly record struct BlizzardLease(bool IsAcquired) : IDisposable
{
    public void Dispose() { }
}

public interface IBlizzardRateLimiter
{
    ValueTask<BlizzardLease> AcquireAsync(CancellationToken ct);
    void PauseUntil(DateTimeOffset until);
}
