using Microsoft.Azure.Cosmos;

namespace Lfm.E2E.Seeds;

public static class DefaultSeed
{
    public static async Task SeedAsync(CosmosClient client, string databaseName)
    {
        var dbResponse = await RetryAsync(
            () => client.CreateDatabaseIfNotExistsAsync(databaseName));
        var db = dbResponse.Database;

        var containerResponse = await RetryAsync(
            () => db.CreateContainerIfNotExistsAsync(
                new ContainerProperties("raiders", "/battleNetId")));
        var container = containerResponse.Container;

        var raider = new Dictionary<string, object?>
        {
            ["id"] = "test-bnet-id",
            ["battleNetId"] = "test-bnet-id",
            ["selectedCharacterId"] = "eu-test-realm-aelrin",
            ["locale"] = null,
            ["lastSeenAt"] = "2026-03-18T12:00:00.0000000Z",
            ["characters"] = new List<object>(),
        };

        await RetryAsync(
            () => container.UpsertItemAsync(raider, new PartitionKey("test-bnet-id")));
    }

    private static async Task<T> RetryAsync<T>(Func<Task<T>> action, int maxRetries = 5)
    {
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                return await action();
            }
            catch (CosmosException) when (i < maxRetries - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(2 * (i + 1)));
            }
            catch (HttpRequestException) when (i < maxRetries - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(2 * (i + 1)));
            }
        }
        return await action();
    }
}
