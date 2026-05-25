// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.Services;

internal sealed class ReferenceListCache<T>
{
    private readonly object _sync = new();
    private Task<IReadOnlyList<T>>? _items;

    public Task<IReadOnlyList<T>> GetOrLoadAsync(
        Func<CancellationToken, Task<IReadOnlyList<T>>> loadAsync,
        CancellationToken ct)
    {
        lock (_sync)
        {
            if (_items is not null)
                return _items;

            _items = LoadAndResetOnFailureAsync(loadAsync, ct);
            return _items;
        }
    }

    private async Task<IReadOnlyList<T>> LoadAndResetOnFailureAsync(
        Func<CancellationToken, Task<IReadOnlyList<T>>> loadAsync,
        CancellationToken ct)
    {
        try
        {
            return await loadAsync(ct);
        }
        catch
        {
            lock (_sync)
            {
                _items = null;
            }

            throw;
        }
    }
}
