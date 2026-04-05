using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Lfm.Api.Options;

namespace Lfm.Api.Repositories;

public sealed class RunsRepository(CosmosClient client, IOptions<CosmosOptions> cosmosOpts) : IRunsRepository
{
    private const string ContainerName = "runs";
    private readonly Container _container = client.GetContainer(cosmosOpts.Value.DatabaseName, ContainerName);

    public async Task ScrubRaiderAsync(string battleNetId, CancellationToken ct)
    {
        // Cross-partition query: find runs where this raider is creator or participant.
        // Mirrors the TS query in scrubRaiderFromRuns (raider-cleanup.ts).
        const string query = """
            SELECT * FROM c
            WHERE c.creatorBattleNetId = @battleNetId
               OR ARRAY_CONTAINS(c.runCharacters, {"raiderBattleNetId": @battleNetId}, true)
            """;

        var feedIterator = _container.GetItemQueryIterator<RunDocument>(
            new QueryDefinition(query)
                .WithParameter("@battleNetId", battleNetId));

        var runs = new List<RunDocument>();
        while (feedIterator.HasMoreResults)
        {
            var page = await feedIterator.ReadNextAsync(ct);
            runs.AddRange(page);
        }

        // Replace each modified run. TS uses Promise.all; sequential is fine at hobby scale.
        foreach (var run in runs)
        {
            var scrubbed = ScrubRunDocument(run, battleNetId);
            if (scrubbed.Modified)
            {
                await _container.ReplaceItemAsync(
                    scrubbed.Run,
                    run.Id,
                    new PartitionKey(run.Id),
                    cancellationToken: ct);
            }
        }
    }

    /// <summary>
    /// Removes all runCharacters entries for the given battleNetId and nulls
    /// creatorBattleNetId when it matches. Returns the (possibly unchanged) run
    /// and a flag indicating whether any change was made.
    /// Mirrors scrubRunDocument in raider-cleanup.ts.
    /// </summary>
    private static (bool Modified, RunDocument Run) ScrubRunDocument(RunDocument run, string battleNetId)
    {
        var filteredCharacters = run.RunCharacters
            .Where(rc => rc.RaiderBattleNetId != battleNetId)
            .ToList();

        var charactersScrubbed = filteredCharacters.Count != run.RunCharacters.Count;

        string? newCreator = run.CreatorBattleNetId == battleNetId ? null : run.CreatorBattleNetId;
        var creatorScrubbed = newCreator != run.CreatorBattleNetId;

        if (!charactersScrubbed && !creatorScrubbed)
            return (false, run);

        return (true, run with
        {
            CreatorBattleNetId = newCreator,
            RunCharacters = filteredCharacters,
        });
    }
}
