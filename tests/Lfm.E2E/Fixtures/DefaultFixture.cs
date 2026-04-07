using Lfm.E2E.Seeds;
using Xunit;

namespace Lfm.E2E.Fixtures;

[CollectionDefinition("Default")]
public class DefaultCollection : ICollectionFixture<DefaultFixture> { }

public class DefaultFixture : IAsyncLifetime
{
    public StackFixture Stack { get; } = new();

    public async Task InitializeAsync()
    {
        await Stack.InitializeAsync();
        await DefaultSeed.SeedAsync(Stack.CosmosClient, StackFixture.DatabaseName);
    }

    public Task DisposeAsync() => Stack.DisposeAsync();
}
