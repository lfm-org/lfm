using Lfm.E2E.Infrastructure;
using Lfm.E2E.Seeds;
using Xunit;

namespace Lfm.E2E.Fixtures;

[CollectionDefinition("Runs")]
public class RunsCollection : ICollectionFixture<RunsFixture> { }

public class RunsFixture : IAsyncLifetime
{
    public StackFixture Stack { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Stack = await SharedStack.GetAsync();
        await DefaultSeed.SeedAsync(Stack.CosmosClient, StackFixture.DatabaseName);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
