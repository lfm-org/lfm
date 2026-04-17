// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.Services;

public interface IDataCache
{
    void Invalidate(string key);
    event Action<string>? OnInvalidated;
}
