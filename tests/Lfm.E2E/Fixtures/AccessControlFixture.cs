// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.E2E.Infrastructure;
using Xunit;

namespace Lfm.E2E.Fixtures;

[CollectionDefinition("AccessControl")]
public class AccessControlCollection : ICollectionFixture<AccessControlFixture> { }

public class AccessControlFixture : IAsyncLifetime
{
    public StackFixture Stack { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Stack = await SharedStack.GetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
