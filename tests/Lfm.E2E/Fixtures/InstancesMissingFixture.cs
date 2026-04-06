using Xunit;

namespace Lfm.E2E.Fixtures;

[CollectionDefinition("instances-missing")]
public class InstancesMissingScenarioCollection : ICollectionFixture<InstancesMissingFixture> { }

/// <summary>
/// No instances or specializations seeded. All other data (guilds, raiders, runs) is present.
/// Tests the empty-instances-list UI state (e.g. create-run form shows no instance options).
/// </summary>
public class InstancesMissingFixture : StackFixture
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await Seeds.SeedBuilders.SeedInstancesMissingAsync(Cosmos.GetConnectionString());
    }
}
