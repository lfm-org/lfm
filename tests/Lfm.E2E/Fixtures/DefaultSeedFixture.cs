using Xunit;

namespace Lfm.E2E.Fixtures;

[CollectionDefinition("default")]
public class DefaultScenarioCollection : ICollectionFixture<DefaultSeedFixture> { }

/// <summary>
/// Full data: instances, specializations, guilds, raiders, and runs (including one with signups).
/// </summary>
public class DefaultSeedFixture : StackFixture
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await Seeds.SeedBuilders.SeedDefaultAsync(Cosmos.GetConnectionString());
    }
}
