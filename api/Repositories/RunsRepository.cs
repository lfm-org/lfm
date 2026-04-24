// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Lfm.Api.Helpers;
using Lfm.Api.Options;

namespace Lfm.Api.Repositories;

public sealed class RunsRepository(CosmosClient client, IOptions<CosmosOptions> cosmosOpts) : IRunsRepository
{
    private const string ContainerName = "runs";
    private readonly Container _container = client.GetContainer(cosmosOpts.Value.DatabaseName, ContainerName);

    public async Task<RunsPage> ListForGuildAsync(
        string guildId, string battleNetId, int top, string? continuationToken, CancellationToken ct)
    {
        // Mirrors the guild-scoped query in runs-list.ts:
        //   PUBLIC runs | runs created by this user | GUILD runs from the same guild
        //   Ordered by startTime ascending.
        if (!int.TryParse(guildId, out var numericGuildId))
            return new RunsPage(Array.Empty<RunDocument>(), null);

        const string query = """
            SELECT * FROM c
            WHERE c.visibility = 'PUBLIC'
               OR c.creatorBattleNetId = @battleNetId
               OR (c.visibility = 'GUILD' AND c.creatorGuildId = @guildId)
            ORDER BY c.startTime ASC
            """;

        return await QueryOnePageAsync(
            new QueryDefinition(query)
                .WithParameter("@battleNetId", battleNetId)
                .WithParameter("@guildId", numericGuildId),
            top,
            continuationToken,
            ct);
    }

    public async Task<RunsPage> ListForUserAsync(
        string battleNetId, int top, string? continuationToken, CancellationToken ct)
    {
        // Mirrors the no-guild branch in runs-list.ts:
        //   PUBLIC runs | runs created by this user
        //   Ordered by startTime ascending.
        const string query = """
            SELECT * FROM c
            WHERE c.visibility = 'PUBLIC'
               OR c.creatorBattleNetId = @battleNetId
            ORDER BY c.startTime ASC
            """;

        return await QueryOnePageAsync(
            new QueryDefinition(query)
                .WithParameter("@battleNetId", battleNetId),
            top,
            continuationToken,
            ct);
    }

    // Reads exactly one page (up to `top` items) from Cosmos and returns it
    // alongside the continuation token. Does NOT drain the iterator — the caller
    // re-invokes us with the returned token to fetch the next page.
    private async Task<RunsPage> QueryOnePageAsync(
        QueryDefinition queryDef, int top, string? continuationToken, CancellationToken ct)
    {
        var options = new QueryRequestOptions { MaxItemCount = top };
        var feedIterator = _container.GetItemQueryIterator<RunDocument>(
            queryDef, continuationToken, options);

        if (!feedIterator.HasMoreResults)
            return new RunsPage(Array.Empty<RunDocument>(), null);

        var response = await feedIterator.ReadNextAsync(ct);
        var items = response.ToList();
        return new RunsPage(items, response.ContinuationToken);
    }

    public async Task<RunDocument?> GetByIdAsync(string id, CancellationToken ct)
    {
        try
        {
            // Point read: partition key == id (as stored in the "runs" container).
            // Mirrors: container.item(id, id).read<RunDocument>() in runs-detail.ts.
            var response = await _container.ReadItemAsync<RunDocument>(
                id,
                new PartitionKey(id),
                cancellationToken: ct);
            return response.Resource with { ETag = response.ETag };
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<RunDocument> CreateAsync(RunDocument run, CancellationToken ct)
    {
        var response = await _container.CreateItemAsync(
            run,
            new PartitionKey(run.Id),
            cancellationToken: ct);
        return response.Resource;
    }

    public async Task<RunDocument> UpdateAsync(RunDocument run, string? ifMatchEtag, CancellationToken ct)
    {
        try
        {
            // An explicit ifMatchEtag (from a client If-Match header) always wins;
            // fall back to the document's stored ETag for internal retry loops
            // (e.g. RunsSignupFunction) that re-fetch the run each attempt.
            var options = new ItemRequestOptions { IfMatchEtag = ifMatchEtag ?? run.ETag };
            var response = await _container.ReplaceItemAsync(
                run,
                run.Id,
                new PartitionKey(run.Id),
                options,
                ct);
            return response.Resource with { ETag = response.ETag };
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            throw new ConcurrencyConflictException(ex);
        }
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        try
        {
            await _container.DeleteItemAsync<RunDocument>(
                id,
                new PartitionKey(id),
                cancellationToken: ct);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Idempotent: document already gone — treat as success.
        }
    }

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

    public async Task<RunMigrationResult> MigrateSchemaAsync(bool dryRun, CancellationToken ct)
    {
        // Iterate every run document. The set is small (hobby scale), and
        // unlike ScrubRaiderAsync we don't have a cheap WHERE predicate —
        // every legacy doc needs inspection.
        var feedIterator = _container.GetItemQueryIterator<RunDocument>("SELECT * FROM c");

        var scanned = 0;
        var migrated = 0;
        while (feedIterator.HasMoreResults)
        {
            var page = await feedIterator.ReadNextAsync(ct);
            foreach (var run in page)
            {
                scanned++;
                var populated = RunModeResolver.EnsurePopulated(run);
                if (ReferenceEquals(populated, run)) continue;
                migrated++;
                if (!dryRun)
                {
                    await _container.ReplaceItemAsync(
                        populated,
                        run.Id,
                        new PartitionKey(run.Id),
                        cancellationToken: ct);
                }
            }
        }
        return new RunMigrationResult(scanned, migrated, dryRun);
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
