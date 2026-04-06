using Xunit;

namespace Lfm.E2E.Fixtures;

[CollectionDefinition("runs-empty")]
public class RunsEmptyScenarioCollection : ICollectionFixture<RunsEmptyFixture> { }

/// <summary>
/// Instances, specializations, guilds, and raiders are seeded, but NO run documents.
/// The runs container exists and is empty. Tests the empty-runs-list UI state.
/// </summary>
public class RunsEmptyFixture : StackFixture
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await Seeds.SeedBuilders.SeedRunsEmptyAsync(Cosmos.GetConnectionString());
    }
}
