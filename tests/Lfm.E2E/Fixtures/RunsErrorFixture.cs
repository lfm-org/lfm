using Xunit;

namespace Lfm.E2E.Fixtures;

[CollectionDefinition("runs-error")]
public class RunsErrorScenarioCollection : ICollectionFixture<RunsErrorFixture> { }

/// <summary>
/// Like default, but the "runs" container is not created at all.
/// Any runs API call will produce a CosmosException (container not found),
/// causing the API to return an error response. Tests the runs-error UI state.
/// Mirrors the TS "raids-error" scenario where getRunsContainerDefinitionForScenario returns null.
/// </summary>
public class RunsErrorFixture : StackFixture
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await Seeds.SeedBuilders.SeedRunsErrorAsync(Cosmos.GetConnectionString());
    }
}
