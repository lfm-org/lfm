// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Cosmos;

namespace Lfm.E2E.Seeds;

public static class DefaultSeed
{
    // Well-known test identifiers shared with AuthHelper and spec files.
    public const string PrimaryBattleNetId = "test-bnet-id";
    public const string SecondaryBattleNetId = "test-bnet-id-2";
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

        // --- Instances container (partition key: /id) ---
        var instancesContainer = (await RetryAsync(
            () => db.CreateContainerIfNotExistsAsync(
                new ContainerProperties("instances", "/id")))).Container;

        await SeedInstancesAsync(instancesContainer);

        // --- Specializations container (partition key: /id) ---
        await RetryAsync(
            () => db.CreateContainerIfNotExistsAsync(
                new ContainerProperties("specializations", "/id")));
    }

    private static async Task SeedPrimaryRaiderAsync(Container container)
    {
        var raider = new Dictionary<string, object?>
        {
            ["id"] = PrimaryBattleNetId,
            ["battleNetId"] = PrimaryBattleNetId,
            ["selectedCharacterId"] = "eu-test-realm-aelrin",
            ["locale"] = null,
            ["lastSeenAt"] = "2026-03-18T12:00:00.0000000Z",
            // ttl must be a valid integer — Cosmos rejects "ttl": null on upsert.
            ["ttl"] = -1,
            ["accountProfileRefreshedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["accountProfileFetchedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["characters"] = new List<object>
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "eu-test-realm-aelrin",
                    ["region"] = "eu",
                    ["realm"] = "test-realm",
                    ["name"] = "Aelrin",
                    ["portraitUrl"] = null,
                    ["specializationsSummary"] = new Dictionary<string, object?>
                    {
                        ["activeSpecialization"] = new Dictionary<string, object?>
                        {
                            ["id"] = 62,
                            ["name"] = "Arcane",
                        },
                        ["specializations"] = new List<object>
                        {
                            new Dictionary<string, object?>
                            {
                                ["specialization"] = new Dictionary<string, object?>
                                {
                                    ["id"] = 62,
                                    ["name"] = "Arcane",
                                },
                            },
                        },
                    },
                    ["guildId"] = 12345,
                    ["guildName"] = "Test Guild",
                },
                // Second character on the primary raider — used by SelectCharacter tests.
                new Dictionary<string, object?>
                {
                    ["id"] = "eu-test-realm-aelrin-alt",
                    ["region"] = "eu",
                    ["realm"] = "test-realm",
                    ["name"] = "Aelrinalt",
                    ["portraitUrl"] = null,
                    ["specializationsSummary"] = new Dictionary<string, object?>
                    {
                        ["activeSpecialization"] = new Dictionary<string, object?>
                        {
                            ["id"] = 65,
                            ["name"] = "Holy",
                        },
                        ["specializations"] = new List<object>
                        {
                            new Dictionary<string, object?>
                            {
                                ["specialization"] = new Dictionary<string, object?>
                                {
                                    ["id"] = 65,
                                    ["name"] = "Holy",
                                },
                            },
                        },
                    },
                    ["guildId"] = 12345,
                    ["guildName"] = "Test Guild",
                },
            },
            ["accountProfileSummary"] = new Dictionary<string, object?>
            {
                ["wow_accounts"] = new List<object>
                {
                    new Dictionary<string, object?>
                    {
                        ["id"] = 1,
                        ["characters"] = new List<object>
                        {
                            new Dictionary<string, object?>
                            {
                                ["name"] = "Aelrin",
                                ["level"] = 80,
                                ["realm"] = new Dictionary<string, object?>
                                {
                                    ["slug"] = "test-realm",
                                    ["name"] = "Test Realm",
                                },
                                ["playable_class"] = new Dictionary<string, object?>
                                {
                                    ["id"] = 8,
                                    ["name"] = "Mage",
                                },
                            },
                            // Keep in sync with raider.characters above: the characters
                            // endpoint iterates wow_accounts[*].characters to build the
                            // card list, so a mismatch here shows as missing cards in
                            // the UI and drove CharactersPage_Loads_DisplaysCharacterList
                            // to tolerate an empty render as a pass.
                            new Dictionary<string, object?>
                            {
                                ["name"] = "Aelrinalt",
                                ["level"] = 80,
                                ["realm"] = new Dictionary<string, object?>
                                {
                                    ["slug"] = "test-realm",
                                    ["name"] = "Test Realm",
                                },
                                ["playable_class"] = new Dictionary<string, object?>
                                {
                                    ["id"] = 2,
                                    ["name"] = "Paladin",
                                },
                            },
                        },
                    },
                },
            },
        };

        await RetryAsync(
            () => container.UpsertItemAsync(raider, new PartitionKey(PrimaryBattleNetId)));
    }

    private static async Task SeedSecondaryRaiderAsync(Container container)
    {
        var raider = new Dictionary<string, object?>
        {
            ["id"] = SecondaryBattleNetId,
            ["battleNetId"] = SecondaryBattleNetId,
            ["selectedCharacterId"] = "eu-test-realm-kaldris",
            ["locale"] = null,
            ["lastSeenAt"] = "2026-03-18T12:00:00.0000000Z",
            // ttl must be a valid integer — Cosmos rejects "ttl": null on upsert.
            ["ttl"] = -1,
            ["accountProfileRefreshedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["accountProfileFetchedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["characters"] = new List<object>
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "eu-test-realm-kaldris",
                    ["region"] = "eu",
                    ["realm"] = "test-realm",
                    ["name"] = "Kaldris",
                    ["portraitUrl"] = null,
                    ["specializationsSummary"] = new Dictionary<string, object?>
                    {
                        ["activeSpecialization"] = new Dictionary<string, object?>
                        {
                            ["id"] = 71,
                            ["name"] = "Arms",
                        },
                        ["specializations"] = new List<object>
                        {
                            new Dictionary<string, object?>
                            {
                                ["specialization"] = new Dictionary<string, object?>
                                {
                                    ["id"] = 71,
                                    ["name"] = "Arms",
                                },
                            },
                        },
                    },
                    ["guildId"] = 12345,
                    ["guildName"] = "Test Guild",
                },
            },
            ["accountProfileSummary"] = new Dictionary<string, object?>
            {
                ["wow_accounts"] = new List<object>
                {
                    new Dictionary<string, object?>
                    {
                        ["id"] = 2,
                        ["characters"] = new List<object>
                        {
                            new Dictionary<string, object?>
                            {
                                ["name"] = "Kaldris",
                                ["level"] = 80,
                                ["realm"] = new Dictionary<string, object?>
                                {
                                    ["slug"] = "test-realm",
                                    ["name"] = "Test Realm",
                                },
                                ["playable_class"] = new Dictionary<string, object?>
                                {
                                    ["id"] = 1,
                                    ["name"] = "Warrior",
                                },
                            },
                        },
                    },
                },
            },
        };

        await RetryAsync(
            () => container.UpsertItemAsync(raider, new PartitionKey(SecondaryBattleNetId)));
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
            ["visibility"] = "PUBLIC",
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

    private static async Task SeedInstancesAsync(Container container)
    {
        // Matches InstanceDocument schema: id = "{instanceId}:{modeKey}", partition key = /id
        var instance = new Dictionary<string, object?>
        {
            ["id"] = "67:NORMAL:25",
            ["instanceId"] = "67",
            ["name"] = "Liberation of Undermine",
            ["modeKey"] = "NORMAL:25",
            ["expansion"] = "The War Within",
        };

        await RetryAsync(
            () => container.UpsertItemAsync(instance, new PartitionKey("67:NORMAL:25")));
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
