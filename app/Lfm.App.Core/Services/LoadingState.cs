// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.Services;

public abstract record LoadingState<T>
{
    public sealed record Idle : LoadingState<T>;
    public sealed record Loading : LoadingState<T>;
    public sealed record Success(T Value) : LoadingState<T>;
    public sealed record Failure(string Message) : LoadingState<T>;
}
