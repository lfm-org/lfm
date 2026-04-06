using Xunit;

namespace Lfm.E2E.Fixtures;

[CollectionDefinition("characters-empty")]
public class CharactersEmptyScenarioCollection : ICollectionFixture<CharactersEmptyFixture> { }

/// <summary>
/// Full data, but the primary test raider (test-bnet-id) has an empty characters list
/// and selectedCharacterId = null. Tests the no-character UI state.
/// </summary>
public class CharactersEmptyFixture : StackFixture
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await Seeds.SeedBuilders.SeedCharactersEmptyAsync(Cosmos.GetConnectionString());
    }
}
