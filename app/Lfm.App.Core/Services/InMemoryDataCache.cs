// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.Services;

public sealed class InMemoryDataCache : IDataCache
{
    public event Action<string>? OnInvalidated;
    public void Invalidate(string key) => OnInvalidated?.Invoke(key);
}
