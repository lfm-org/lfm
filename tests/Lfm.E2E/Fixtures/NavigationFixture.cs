// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.E2E.Infrastructure;
using Xunit;

namespace Lfm.E2E.Fixtures;

[CollectionDefinition("Navigation")]
public class NavigationCollection : ICollectionFixture<NavigationFixture> { }

public class NavigationFixture : IAsyncLifetime
{
    public StackFixture Stack { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Stack = await SharedStack.GetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
