// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Cosmos;

namespace Lfm.E2E.Seeds;

public static class DefaultSeed
{
    // Well-known test identifiers shared with AuthHelper and spec files.
    public const string PrimaryBattleNetId = "test-bnet-id";
    public const string SecondaryBattleNetId = "test-bnet-id-2";
    public const string DisposableBattleNetId = "test-bnet-id-delete";
    // Must match the guildId assigned by E2ELoginFunction for non-admin test users.
    // Must be numeric — RunsRepository.ListForGuildAsync does int.TryParse on it.
    public const string TestGuildId = "12345";
    public const string TestRunId = "e2e-run-001";

    public static async Task SeedAsync(CosmosClient client, string databaseName)
    {
        var dbResponse = await RetryAsync(
            () => client.CreateDatabaseIfNotExistsAsync(databaseName));
        var db = dbResponse.Database;

        // --- Raiders container (partition key: /battleNetId) ---
        var raidersContainer = (await RetryAsync(
            () => db.CreateContainerIfNotExistsAsync(
                new ContainerProperties("raiders", "/battleNetId")))).Container;

        await SeedPrimaryRaiderAsync(raidersContainer);
        await SeedSecondaryRaiderAsync(raidersContainer);
        await SeedDisposableRaiderAsync(raidersContainer);

        // --- Guilds container (partition key: /id) ---
        var guildsContainer = (await RetryAsync(
            () => db.CreateContainerIfNotExistsAsync(
                new ContainerProperties("guilds", "/id")))).Container;

        await SeedGuildAsync(guildsContainer);

        // --- Runs container (partition key: /id) ---
        var runsContainer = (await RetryAsync(
            () => db.CreateContainerIfNotExistsAsync(
                new ContainerProperties("runs", "/id")))).Container;

        await SeedRunAsync(runsContainer);

        // Reference data (instances, specializations) lives in blob — see
        // docs/storage-architecture.md. Seeded separately via WowReferenceSeed
        // against the Azurite blob container.
    }

    private static async Task SeedPrimaryRaiderAsync(Container container)
    {
        var raider = new RaiderSeedBuilder(PrimaryBattleNetId, accountId: 1)
            .AddCharacter(
                id: "eu-test-realm-aelrin",
                name: "Aelrin",
                classId: 8,
                className: "Mage",
                specializationId: 62,
                specializationName: "Arcane")
            // Second character on the primary raider - used by SelectCharacter tests.
            .AddCharacter(
                id: "eu-test-realm-aelrin-alt",
                name: "Aelrinalt",
                classId: 2,
                className: "Paladin",
                specializationId: 65,
                specializationName: "Holy")
            .Build();

        await RetryAsync(
            () => container.UpsertItemAsync(raider, new PartitionKey(PrimaryBattleNetId)));
    }

    private static async Task SeedSecondaryRaiderAsync(Container container)
    {
        var raider = new RaiderSeedBuilder(SecondaryBattleNetId, accountId: 2)
            .AddCharacter(
                id: "eu-test-realm-kaldris",
                name: "Kaldris",
                classId: 1,
                className: "Warrior",
                specializationId: 71,
                specializationName: "Arms")
            .Build();

        await RetryAsync(
            () => container.UpsertItemAsync(raider, new PartitionKey(SecondaryBattleNetId)));
    }

    private static async Task SeedDisposableRaiderAsync(Container container)
    {
        var raider = new RaiderSeedBuilder(DisposableBattleNetId, accountId: 3)
            .AddCharacter(
                id: "eu-test-realm-thalora",
                name: "Thalora",
                classId: 5,
                className: "Priest",
                specializationId: 257,
                specializationName: "Holy")
            .Build();

        await RetryAsync(
            () => container.UpsertItemAsync(raider, new PartitionKey(DisposableBattleNetId)));
    }

    private static async Task SeedGuildAsync(Container container)
    {
        var guild = new Dictionary<string, object?>
        {
            ["id"] = TestGuildId,       // matches E2ELoginFunction guildId = "test-guild-id"
            ["guildId"] = 12345,        // Blizzard integer guild id
            ["realmSlug"] = "test-realm",
            ["slogan"] = "E2E test guild",
            ["setup"] = new Dictionary<string, object?>
            {
                ["initializedAt"] = "2026-03-01T00:00:00.0000000Z",
                ["timezone"] = "Europe/London",
                ["locale"] = "en_GB",
            },
            ["rankPermissions"] = new List<object>
            {
                new Dictionary<string, object?>
                {
                    ["rank"] = 0,
                    ["canCreateGuildRuns"] = true,
                    ["canSignupGuildRuns"] = true,
                    ["canDeleteGuildRuns"] = true,
                },
                new Dictionary<string, object?>
                {
                    ["rank"] = 1,
                    ["canCreateGuildRuns"] = true,
                    ["canSignupGuildRuns"] = true,
                    ["canDeleteGuildRuns"] = false,
                },
            },
            ["blizzardProfileRaw"] = new Dictionary<string, object?>
            {
                ["name"] = "Test Guild",
                ["realm"] = new Dictionary<string, object?>
                {
                    ["slug"] = "test-realm",
                    ["name"] = "Test Realm",
                },
                ["faction"] = new Dictionary<string, object?>
                {
                    ["name"] = "Alliance",
                },
                ["memberCount"] = 25,
                ["achievementPoints"] = 1500,
            },
            // Roster makes the primary raider (Aelrin, test-realm) rank 0 (guild master),
            // enabling GuildPermissions.IsAdminAsync to return true for guild admin tests.
            // blizzardRosterFetchedAt is set to "now" so the roster is considered fresh.
            ["blizzardRosterFetchedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["blizzardRosterRaw"] = new Dictionary<string, object?>
            {
                ["members"] = new List<object>
                {
                    new Dictionary<string, object?>
                    {
                        ["rank"] = 0,
                        ["character"] = new Dictionary<string, object?>
                        {
                            ["name"] = "Aelrin",
                            ["realm"] = new Dictionary<string, object?> { ["slug"] = "test-realm" },
                        },
                    },
                    new Dictionary<string, object?>
                    {
                        ["rank"] = 1,
                        ["character"] = new Dictionary<string, object?>
                        {
                            ["name"] = "Kaldris",
                            ["realm"] = new Dictionary<string, object?> { ["slug"] = "test-realm" },
                        },
                    },
                },
            },
        };

        await RetryAsync(
            () => container.UpsertItemAsync(guild, new PartitionKey(TestGuildId)));
    }

    private static async Task SeedRunAsync(Container container)
    {
        // Anchored to UtcNow so the seeded run's signup window is always open
        // relative to the moment the suite runs. Hardcoded timestamps here
        // become time bombs — once `signupCloseTime` slides into the past,
        // RunsUpdateFunction returns 409 ("Editing is closed"), breaking
        // EditRun_ModifyFields_ChangesReflected and any other test that
        // mutates the seeded run.
        var now = DateTimeOffset.UtcNow;
        var startTime = now.AddDays(7);
        var signupCloseTime = startTime.AddMinutes(-30);
        var createdAt = now.AddDays(-14);

        // Match the .fffffffZ shape of the values this seed produced before the
        // time-bomb fix so any downstream string assertions stay stable.
        const string Format = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

        var run = new Dictionary<string, object?>
        {
            ["id"] = TestRunId,
            ["startTime"] = startTime.ToString(Format),
            ["signupCloseTime"] = signupCloseTime.ToString(Format),
            ["description"] = "E2E test run",
            ["modeKey"] = "NORMAL:25",
            ["visibility"] = "GUILD",
            ["creatorGuild"] = "Test Guild",
            ["creatorGuildId"] = 12345,
            ["instanceId"] = 67,
            ["instanceName"] = "Liberation of Undermine",
            ["creatorBattleNetId"] = PrimaryBattleNetId,
            ["createdAt"] = createdAt.ToString(Format),
            ["ttl"] = 2592000,
            ["runCharacters"] = new List<object>
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "signup-001",
                    ["characterId"] = "eu-test-realm-aelrin",
                    ["characterName"] = "Aelrin",
                    ["characterRealm"] = "test-realm",
                    ["characterLevel"] = 80,
                    ["characterClassId"] = 8,
                    ["characterClassName"] = "Mage",
                    ["characterRaceId"] = 1,
                    ["characterRaceName"] = "Human",
                    ["raiderBattleNetId"] = PrimaryBattleNetId,
                    ["desiredAttendance"] = "IN",
                    ["reviewedAttendance"] = "IN",
                    ["specId"] = 62,
                    ["specName"] = "Arcane",
                    ["role"] = "RANGED_DPS",
                },
            },
        };

        await RetryAsync(
            () => container.UpsertItemAsync(run, new PartitionKey(TestRunId)));
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
