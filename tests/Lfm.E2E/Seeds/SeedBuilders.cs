using System.Net;
using Microsoft.Azure.Cosmos;

namespace Lfm.E2E.Seeds;

/// <summary>
/// Seed helpers for each E2E scenario. Each method creates the "lfm-e2e" database,
/// the required containers, and inserts the documents needed for that scenario.
///
/// Document shapes mirror functions/src/scripts/e2e-test-data.ts and
/// functions/src/scripts/e2e-seed-builders.ts.
///
/// Partition keys:
///   instances      → /id  (doc id = "{instanceId}:{modeKey}")
///   specializations → /id  (doc id = string specId)
///   guilds         → /id
///   raiders        → /battleNetId
///   runs           → /id
/// </summary>
public static class SeedBuilders
{
    private const string DatabaseName = "lfm-e2e";
    private const string TestRealm = "test-realm";
    private const string TestRealmName = "Test Realm";
    private const string TestGuildName = "Test Guild";
    private const int TestGuildId = 12345;
    private const string OutsiderGuildName = "Rival Guild";
    private const int OutsiderGuildId = 54321;
    private const int StaleGuildId = 65432;
    private const string StaleGuildName = "Stale Vanguard";
    private const string Region = "eu";

    // BattleNet IDs mirroring functions/src/lib/test-mode.ts
    private const string TestBnetId = "test-bnet-id";
    private const string NeedsCharacterBnetId = "test-bnet-id-needs-character";
    private const string GuildMasterBnetId = "test-bnet-id-guild-master";
    private const string SiteAdminBnetId = "test-bnet-id-admin";
    private const string DeleteAccountBnetId = "test-bnet-id-delete-account";

    // Fixed seed timestamp — deterministic across all scenarios.
    private static readonly DateTime SeedNow = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    // ---------------------------------------------------------------------------
    // Public scenario entry points
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Default: full data — instances, specializations, guilds, raiders, and runs.
    /// </summary>
    public static Task SeedDefaultAsync(string connectionString) =>
        SeedInternalAsync(connectionString, seedInstances: true, seedRuns: true, seedRunsContainer: true, charactersEmpty: false);

    /// <summary>
    /// RunsEmpty: instances + specializations + guilds + raiders, but NO run documents.
    /// </summary>
    public static Task SeedRunsEmptyAsync(string connectionString) =>
        SeedInternalAsync(connectionString, seedInstances: true, seedRuns: false, seedRunsContainer: true, charactersEmpty: false);

    /// <summary>
    /// RunsError: like default but the "runs" container is not created at all,
    /// so any runs API call will fail with a CosmosException.
    /// Mirrors getRunsContainerDefinitionForScenario returning null for "raids-error".
    /// </summary>
    public static Task SeedRunsErrorAsync(string connectionString) =>
        SeedInternalAsync(connectionString, seedInstances: true, seedRuns: false, seedRunsContainer: false, charactersEmpty: false);

    /// <summary>
    /// CharactersEmpty: full data, but the primary test raider has an empty characters list
    /// and selectedCharacterId = null.
    /// </summary>
    public static Task SeedCharactersEmptyAsync(string connectionString) =>
        SeedInternalAsync(connectionString, seedInstances: true, seedRuns: true, seedRunsContainer: true, charactersEmpty: true);

    /// <summary>
    /// InstancesMissing: no instances container/data seeded; all other data is present.
    /// </summary>
    public static Task SeedInstancesMissingAsync(string connectionString) =>
        SeedInternalAsync(connectionString, seedInstances: false, seedRuns: true, seedRunsContainer: true, charactersEmpty: false);

    // ---------------------------------------------------------------------------
    // Internal implementation
    // ---------------------------------------------------------------------------

    private static async Task SeedInternalAsync(
        string connectionString,
        bool seedInstances,
        bool seedRuns,
        bool seedRunsContainer,
        bool charactersEmpty)
    {
        var client = new CosmosClient(
            connectionString,
            new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                // The Linux Cosmos DB emulator can take 2-3 min to warm up on CI.
                // The SDK's GatewayStoreClient has an internal 65s timeout that can't
                // be overridden directly. Setting RequestTimeout higher than 65s helps
                // with some paths, but the real fix is retry logic (see RetryCosmosAsync).
                RequestTimeout = TimeSpan.FromSeconds(180),
                // Increase max connection limit for gateway mode (default 50).
                GatewayModeMaxConnectionLimit = 10,
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                },
                // The Linux Cosmos DB emulator uses a self-signed TLS certificate.
                // Bypass validation in E2E tests only — never in production code.
                HttpClientFactory = () =>
                {
                    var handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                    };
                    return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
                },
            });

        // The emulator may still be warming up — retry all Cosmos operations.
        var dbResponse = await RetryCosmosAsync(() => client.CreateDatabaseIfNotExistsAsync(DatabaseName));
        var db = dbResponse.Database;

        var createdAt = SeedNow.AddHours(-72).ToString("O");

        if (seedInstances)
        {
            await SeedInstancesAsync(db);
            await SeedSpecializationsAsync(db);
        }

        var raiders = BuildRaiders(createdAt, charactersEmpty);
        await SeedRaidersAsync(db, raiders);

        var guilds = BuildGuilds(createdAt);
        await SeedGuildsAsync(db, guilds);

        if (seedRunsContainer)
        {
            var runsContainerResponse = await RetryCosmosAsync(() =>
                db.CreateContainerIfNotExistsAsync(new ContainerProperties("runs", "/id")));
            var runsContainer = runsContainerResponse.Container;

            if (seedRuns)
            {
                var runs = BuildRuns(createdAt, raiders);
                foreach (var run in runs)
                {
                    var runDict = (Dictionary<string, object?>)run;
                    var runId = (string)runDict["id"]!;
                    await RetryCosmosAsync(() => runsContainer.UpsertItemAsync(run, new PartitionKey(runId)));
                }
            }
        }
    }

    // ---------------------------------------------------------------------------
    // Instances + specializations
    // ---------------------------------------------------------------------------

    private static async Task SeedInstancesAsync(Database db)
    {
        var container = (await RetryCosmosAsync(() =>
            db.CreateContainerIfNotExistsAsync(new ContainerProperties("instances", "/id")))).Container;

        foreach (var doc in BuildInstanceDocuments())
        {
            await RetryCosmosAsync(() => container.UpsertItemAsync(doc, new PartitionKey((string)doc["id"]!)));
        }
    }

    private static async Task SeedSpecializationsAsync(Database db)
    {
        var container = (await RetryCosmosAsync(() =>
            db.CreateContainerIfNotExistsAsync(new ContainerProperties("specializations", "/id")))).Container;

        foreach (var doc in BuildSpecializationDocuments())
        {
            await RetryCosmosAsync(() => container.UpsertItemAsync(doc, new PartitionKey((string)doc["id"]!)));
        }
    }

    // ---------------------------------------------------------------------------
    // Raiders
    // ---------------------------------------------------------------------------

    private static async Task SeedRaidersAsync(Database db, List<object> raiders)
    {
        var container = (await RetryCosmosAsync(() =>
            db.CreateContainerIfNotExistsAsync(new ContainerProperties("raiders", "/battleNetId")))).Container;

        foreach (var raider in raiders)
        {
            // Each raider document is a Dictionary; battleNetId is also the id.
            var dict = (Dictionary<string, object?>)raider;
            var battleNetId = (string)dict["battleNetId"]!;
            await RetryCosmosAsync(() => container.UpsertItemAsync(raider, new PartitionKey(battleNetId)));
        }
    }

    // ---------------------------------------------------------------------------
    // Guilds
    // ---------------------------------------------------------------------------

    private static async Task SeedGuildsAsync(Database db, List<object> guilds)
    {
        var container = (await RetryCosmosAsync(() =>
            db.CreateContainerIfNotExistsAsync(new ContainerProperties("guilds", "/id")))).Container;

        foreach (var guild in guilds)
        {
            var dict = (Dictionary<string, object?>)guild;
            var id = (string)dict["id"]!;
            await RetryCosmosAsync(() => container.UpsertItemAsync(guild, new PartitionKey(id)));
        }
    }

    // ---------------------------------------------------------------------------
    // Data builders
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds InstanceDocument-shaped dictionaries for all instances and their modes.
    /// Each (instanceId, modeKey) pair produces one document with id="{instanceId}:{modeKey}".
    /// Mirrors IInstancesRepository — document shape must match InstanceDocument record.
    /// </summary>
    private static IEnumerable<Dictionary<string, object?>> BuildInstanceDocuments()
    {
        // Instances from functions/test-data/wow/instances.json
        var instances = new[]
        {
            new { Id = 63, Name = "Deadmines", Expansion = "Classic", Modes = new[] { ("NORMAL", 5), ("HEROIC", 5) } },
            new { Id = 249, Name = "Onyxia's Lair", Expansion = "Wrath of the Lich King", Modes = new[] { ("NORMAL", 25) } },
            new { Id = 631, Name = "Icecrown Citadel", Expansion = "Wrath of the Lich King", Modes = new[] { ("NORMAL", 10), ("NORMAL", 25), ("HEROIC", 10), ("HEROIC", 25) } },
            new { Id = 741, Name = "Molten Core", Expansion = "Classic", Modes = new[] { ("NORMAL", 40) } },
        };

        foreach (var inst in instances)
        {
            var modeKeySeen = new Dictionary<string, int>();
            foreach (var (mode, players) in inst.Modes)
            {
                var modeKey = $"{mode}:{players}";
                var docId = $"{inst.Id}:{modeKey}";
                yield return new Dictionary<string, object?>
                {
                    ["id"] = docId,
                    ["instanceId"] = inst.Id.ToString(),
                    ["name"] = inst.Name,
                    ["modeKey"] = modeKey,
                    ["expansion"] = inst.Expansion,
                };
            }
        }
    }

    /// <summary>
    /// Builds SpecializationDocument-shaped dictionaries.
    /// From functions/test-data/wow/specializations.json.
    /// </summary>
    private static IEnumerable<Dictionary<string, object?>> BuildSpecializationDocuments()
    {
        var specs = new[]
        {
            new { Id = 72, Name = "Fury", ClassId = 1, Role = "DPS" },
            new { Id = 73, Name = "Protection", ClassId = 1, Role = "TANK" },
            new { Id = 65, Name = "Holy", ClassId = 2, Role = "HEALER" },
            new { Id = 66, Name = "Protection", ClassId = 2, Role = "TANK" },
            new { Id = 70, Name = "Retribution", ClassId = 2, Role = "DPS" },
            new { Id = 254, Name = "Marksmanship", ClassId = 3, Role = "DPS" },
            new { Id = 255, Name = "Survival", ClassId = 3, Role = "DPS" },
            new { Id = 256, Name = "Discipline", ClassId = 5, Role = "HEALER" },
            new { Id = 258, Name = "Shadow", ClassId = 5, Role = "DPS" },
            new { Id = 250, Name = "Blood", ClassId = 6, Role = "TANK" },
            new { Id = 252, Name = "Unholy", ClassId = 6, Role = "DPS" },
            new { Id = 262, Name = "Elemental", ClassId = 7, Role = "DPS" },
            new { Id = 264, Name = "Restoration", ClassId = 7, Role = "HEALER" },
            new { Id = 63, Name = "Fire", ClassId = 8, Role = "DPS" },
            new { Id = 64, Name = "Frost", ClassId = 8, Role = "DPS" },
            new { Id = 265, Name = "Affliction", ClassId = 9, Role = "DPS" },
            new { Id = 267, Name = "Destruction", ClassId = 9, Role = "DPS" },
            new { Id = 268, Name = "Brewmaster", ClassId = 10, Role = "TANK" },
            new { Id = 270, Name = "Mistweaver", ClassId = 10, Role = "HEALER" },
            new { Id = 102, Name = "Balance", ClassId = 11, Role = "DPS" },
            new { Id = 104, Name = "Guardian", ClassId = 11, Role = "TANK" },
            new { Id = 105, Name = "Restoration", ClassId = 11, Role = "HEALER" },
        };

        foreach (var s in specs)
        {
            yield return new Dictionary<string, object?>
            {
                ["id"] = s.Id.ToString(),
                ["specId"] = s.Id,
                ["name"] = s.Name,
                ["classId"] = s.ClassId,
                ["role"] = s.Role,
                ["iconUrl"] = (string?)null,
            };
        }
    }

    // Character templates — mirrors CHARACTER_TEMPLATES in e2e-seed-builders.ts (indices used below).
    private static readonly (int ClassId, string ClassName, int RaceId, string RaceName, int[] SpecIds, int ActiveSpecId)[] CharacterTemplates =
    [
        (1,  "Warrior",      1,  "Human",     [73, 72],  73),  // 0
        (2,  "Paladin",      3,  "Dwarf",     [70, 66],  70),  // 1
        (2,  "Paladin",      11, "Draenei",   [65, 66],  65),  // 2
        (5,  "Priest",       1,  "Human",     [256, 258], 256), // 3
        (7,  "Shaman",       2,  "Orc",       [264, 262], 264), // 4
        (8,  "Mage",         7,  "Gnome",     [63, 64],  63),  // 5
        (11, "Druid",        4,  "Night Elf", [104, 102], 104), // 6
        (3,  "Hunter",       3,  "Dwarf",     [254, 255], 254), // 7
        (6,  "Death Knight", 5,  "Undead",    [250, 252], 250), // 8
        (10, "Monk",         1,  "Human",     [268, 270], 268), // 9
        (9,  "Warlock",      10, "Blood Elf", [265, 267], 265), // 10
        (11, "Druid",        6,  "Tauren",    [105, 102], 105), // 11
    ];

    private static readonly string[] NamePrefixes = ["Cael", "Dorn", "Eira", "Fenn", "Garr", "Hale", "Iria", "Jorn", "Kael", "Lysa"];
    private static readonly string[] NameSuffixes = ["dor", "wyn", "mere", "thorn"];

    private static string GenerateName(int index)
    {
        var prefix = NamePrefixes[index % NamePrefixes.Length];
        var suffix = NameSuffixes[(index / NamePrefixes.Length) % NameSuffixes.Length];
        return prefix + suffix;
    }

    private static string BuildCharacterId(string name) => $"{Region}-{TestRealm}-{name.ToLowerInvariant()}";

    private static Dictionary<string, object?> BuildCharacter(string name, int classId, int raceId, int[] specIds, int activeSpecId)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = BuildCharacterId(name),
            ["region"] = Region,
            ["realm"] = TestRealm,
            ["name"] = name,
            ["level"] = 80,
            ["classId"] = classId,
            ["raceId"] = raceId,
            ["portraitUrl"] = $"https://example.test/portraits/{name.ToLowerInvariant()}.jpg",
            ["fetchedAt"] = "2026-03-18T12:00:00.000Z",
            ["specializations"] = specIds.Select(id => new Dictionary<string, object?> { ["id"] = id }).ToList(),
            ["activeSpecId"] = activeSpecId,
        };
    }

    private static Dictionary<string, object?> BuildStoredCharacter(string name, int classId, int raceId, int[] specIds, int activeSpecId, int? guildId, string? guildName)
    {
        var charId = BuildCharacterId(name);
        var specsWithNames = BuildSpecNamesForIds(specIds);

        var profileSummary = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["level"] = 80,
            ["realm"] = new Dictionary<string, object?> { ["slug"] = TestRealm, ["name"] = new Dictionary<string, object?> { ["en_US"] = TestRealmName } },
            ["character_class"] = new Dictionary<string, object?> { ["id"] = classId, ["name"] = "" },
            ["race"] = new Dictionary<string, object?> { ["id"] = raceId, ["name"] = "" },
        };

        if (guildId.HasValue && guildName is not null)
        {
            profileSummary["guild"] = new Dictionary<string, object?> { ["id"] = guildId, ["name"] = guildName };
        }

        return new Dictionary<string, object?>
        {
            ["id"] = charId,
            ["region"] = Region,
            ["realm"] = TestRealm,
            ["name"] = name,
            ["portraitUrl"] = $"https://example.test/portraits/{name.ToLowerInvariant()}.jpg",
            ["fetchedAt"] = "2026-03-18T12:00:00.000Z",
            ["profileSummary"] = profileSummary,
            ["mediaSummary"] = new Dictionary<string, object?>
            {
                ["assets"] = new[] { new Dictionary<string, object?> { ["key"] = "avatar", ["value"] = $"https://example.test/portraits/{name.ToLowerInvariant()}.jpg" } },
            },
            ["specializationsSummary"] = new Dictionary<string, object?>
            {
                ["specializations"] = specsWithNames.Select(s => new Dictionary<string, object?> { ["specialization"] = new Dictionary<string, object?> { ["id"] = s.Id, ["name"] = s.Name } }).ToList(),
                ["active_specialization"] = specsWithNames.FirstOrDefault(s => s.Id == activeSpecId) is { } active
                    ? new Dictionary<string, object?> { ["id"] = active.Id, ["name"] = active.Name }
                    : null,
            },
        };
    }

    private static List<(int Id, string Name)> BuildSpecNamesForIds(int[] specIds)
    {
        var specMap = new Dictionary<int, string>
        {
            [72] = "Fury",
            [73] = "Protection",
            [65] = "Holy",
            [66] = "Protection",
            [70] = "Retribution",
            [254] = "Marksmanship",
            [255] = "Survival",
            [256] = "Discipline",
            [258] = "Shadow",
            [250] = "Blood",
            [252] = "Unholy",
            [262] = "Elemental",
            [264] = "Restoration",
            [63] = "Fire",
            [64] = "Frost",
            [265] = "Affliction",
            [267] = "Destruction",
            [268] = "Brewmaster",
            [270] = "Mistweaver",
            [102] = "Balance",
            [104] = "Guardian",
            [105] = "Restoration",
        };
        return specIds.Select(id => (id, specMap.TryGetValue(id, out var n) ? n : "Unknown")).ToList();
    }

    private static Dictionary<string, object?> BuildRaiderDocument(
        string battleNetId, int guildId, string guildName, string createdAt,
        List<(string Name, int TemplateIndex)> characterDefs, bool nullSelectedChar = false, bool emptyChars = false)
    {
        var characters = emptyChars
            ? new List<Dictionary<string, object?>>()
            : characterDefs.Select(def =>
            {
                var t = CharacterTemplates[def.TemplateIndex];
                return BuildStoredCharacter(def.Name, t.ClassId, t.RaceId, t.SpecIds, t.ActiveSpecId, guildId, guildName);
            }).ToList();

        var accountProfileChars = emptyChars
            ? new List<Dictionary<string, object?>>()
            : characterDefs.Select((def, i) =>
            {
                var t = CharacterTemplates[def.TemplateIndex];
                return new Dictionary<string, object?>
                {
                    ["id"] = i + 1,
                    ["name"] = def.Name,
                    ["level"] = 80,
                    ["realm"] = new Dictionary<string, object?> { ["id"] = 1305, ["slug"] = TestRealm, ["name"] = new Dictionary<string, object?> { ["en_US"] = TestRealmName } },
                    ["playable_class"] = new Dictionary<string, object?> { ["id"] = t.ClassId, ["name"] = "" },
                    ["playable_race"] = new Dictionary<string, object?> { ["id"] = t.RaceId, ["name"] = "" },
                    ["faction"] = new Dictionary<string, object?> { ["type"] = "ALLIANCE", ["name"] = "Alliance" },
                    ["gender"] = new Dictionary<string, object?> { ["type"] = "UNKNOWN", ["name"] = "Unknown" },
                };
            }).ToList();

        var selectedCharId = (nullSelectedChar || emptyChars || characterDefs.Count == 0)
            ? null
            : BuildCharacterId(characterDefs[0].Name);

        return new Dictionary<string, object?>
        {
            ["id"] = battleNetId,
            ["battleNetId"] = battleNetId,
            ["selectedCharacterId"] = selectedCharId,
            ["createdAt"] = createdAt,
            ["lastSeenAt"] = createdAt,
            ["characters"] = characters,
            ["accountProfileSummary"] = new Dictionary<string, object?>
            {
                ["wow_accounts"] = new[]
                {
                    new Dictionary<string, object?> { ["id"] = 1, ["characters"] = accountProfileChars },
                },
            },
            ["accountProfileFetchedAt"] = createdAt,
            ["accountProfileRefreshedAt"] = createdAt,
        };
    }

    private static List<object> BuildRaiders(string createdAt, bool charactersEmpty)
    {
        var raiders = new List<object>();

        // Special test identities (mirrors buildRaiderSeeds in e2e-seed-builders.ts)
        var testChars = new List<(string Name, int TemplateIndex)> { ("Aelrin", 1), ("Brakka", 4) };
        var gmChars = new List<(string Name, int TemplateIndex)> { ("Highlord", 1) };
        var adminChars = new List<(string Name, int TemplateIndex)> { ("Observer", 5) };
        var deleteChars = new List<(string Name, int TemplateIndex)> { ("Farewell", 5) };

        // TEST_MODE_IDENTITY — primary test raider (index 0 in guild pool)
        var testRaider = BuildRaiderDocument(TestBnetId, TestGuildId, TestGuildName, createdAt, testChars,
            emptyChars: charactersEmpty);
        raiders.Add(testRaider);

        // TEST_MODE_GUILD_MASTER_IDENTITY
        raiders.Add(BuildRaiderDocument(GuildMasterBnetId, TestGuildId, TestGuildName, createdAt, gmChars));

        // TEST_MODE_NEEDS_CHARACTER_IDENTITY — selectedCharacterId = null
        raiders.Add(BuildRaiderDocument(NeedsCharacterBnetId, TestGuildId, TestGuildName, createdAt, testChars, nullSelectedChar: true));

        // TEST_MODE_SITE_ADMIN_IDENTITY — no guild
        raiders.Add(BuildRaiderDocument(SiteAdminBnetId, 0, "", createdAt, adminChars));

        // TEST_MODE_DELETE_ACCOUNT_IDENTITY
        raiders.Add(BuildRaiderDocument(DeleteAccountBnetId, TestGuildId, TestGuildName, createdAt, deleteChars));

        // 31 guild raiders
        for (var i = 0; i < 31; i++)
        {
            var template = CharacterTemplates[i % CharacterTemplates.Length];
            var name = GenerateName(i);
            var bnetId = $"guild-raider-{(i + 1):D2}";
            raiders.Add(BuildRaiderDocument(bnetId, TestGuildId, TestGuildName, createdAt,
                new List<(string, int)> { (name, i % CharacterTemplates.Length) }));
        }

        // 14 outsider raiders
        for (var i = 0; i < 14; i++)
        {
            var templateIndex = (i + 5) % CharacterTemplates.Length;
            var name = $"Rival{GenerateName(i)}";
            var bnetId = $"outsider-raider-{(i + 1):D2}";
            raiders.Add(BuildRaiderDocument(bnetId, OutsiderGuildId, OutsiderGuildName, createdAt,
                new List<(string, int)> { (name, templateIndex) }));
        }

        return raiders;
    }

    private static List<object> BuildGuilds(string createdAt)
    {
        var staleFetchedAt = SeedNow.AddHours(-2).ToString("O");
        var guild = new Dictionary<string, object?>
        {
            ["id"] = StaleGuildId.ToString(),
            ["guildId"] = StaleGuildId,
            ["realmSlug"] = TestRealm,
            ["slogan"] = "Hold roster until sync returns.",
            ["blizzardProfileRaw"] = new Dictionary<string, object?>
            {
                ["id"] = StaleGuildId,
                ["name"] = StaleGuildName,
                ["achievement_points"] = 2400,
                ["member_count"] = 42,
                ["realm"] = new Dictionary<string, object?> { ["id"] = 559, ["slug"] = TestRealm, ["name"] = new Dictionary<string, object?> { ["en_US"] = TestRealmName } },
                ["faction"] = new Dictionary<string, object?> { ["type"] = "ALLIANCE", ["name"] = "Alliance" },
            },
            ["blizzardProfileFetchedAt"] = staleFetchedAt,
            ["blizzardRosterRaw"] = new Dictionary<string, object?>
            {
                ["guild"] = new Dictionary<string, object?>
                {
                    ["id"] = StaleGuildId,
                    ["name"] = StaleGuildName,
                    ["realm"] = new Dictionary<string, object?> { ["id"] = 559, ["slug"] = TestRealm, ["name"] = new Dictionary<string, object?> { ["en_US"] = TestRealmName } },
                    ["faction"] = new Dictionary<string, object?> { ["type"] = "ALLIANCE", ["name"] = "Alliance" },
                },
                ["members"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["character"] = new Dictionary<string, object?>
                        {
                            ["id"] = 91001, ["name"] = "Archivist",
                            ["realm"] = new Dictionary<string, object?> { ["id"] = 559, ["slug"] = TestRealm, ["name"] = new Dictionary<string, object?> { ["en_US"] = TestRealmName } },
                            ["level"] = 80,
                            ["playable_class"] = new Dictionary<string, object?> { ["id"] = 8 },
                            ["playable_race"] = new Dictionary<string, object?> { ["id"] = 7 },
                            ["faction"] = new Dictionary<string, object?> { ["type"] = "ALLIANCE", ["name"] = "Alliance" },
                        },
                        ["rank"] = 0,
                    },
                    new Dictionary<string, object?>
                    {
                        ["character"] = new Dictionary<string, object?>
                        {
                            ["id"] = 91002, ["name"] = "Quarterline",
                            ["realm"] = new Dictionary<string, object?> { ["id"] = 559, ["slug"] = TestRealm, ["name"] = new Dictionary<string, object?> { ["en_US"] = TestRealmName } },
                            ["level"] = 80,
                            ["playable_class"] = new Dictionary<string, object?> { ["id"] = 2 },
                            ["playable_race"] = new Dictionary<string, object?> { ["id"] = 3 },
                            ["faction"] = new Dictionary<string, object?> { ["type"] = "ALLIANCE", ["name"] = "Alliance" },
                        },
                        ["rank"] = 2,
                    },
                },
            },
            ["blizzardRosterFetchedAt"] = staleFetchedAt,
            ["rankPermissions"] = new[]
            {
                new Dictionary<string, object?> { ["rank"] = 0, ["canCreateGuildRuns"] = true, ["canSignupGuildRuns"] = true },
                new Dictionary<string, object?> { ["rank"] = 2, ["canCreateGuildRuns"] = false, ["canSignupGuildRuns"] = true },
            },
            ["setup"] = new Dictionary<string, object?> { ["initializedAt"] = createdAt, ["timezone"] = "UTC" },
        };

        return [guild];
    }

    // ---------------------------------------------------------------------------
    // Runs
    // ---------------------------------------------------------------------------

    private record RunDef(
        string Id,
        int InstanceId,
        string InstanceName,
        string ModeKey,
        string Visibility,
        string CreatorBnetId,
        string Description,
        double StartHoursFromNow,
        double SignupCloseHoursFromNow,
        int SignupCount,
        bool IsGuildPool,
        bool IncludeTestRaider = false,
        int PoolOffset = 0);

    private static List<object> BuildRuns(string createdAt, List<object> allRaiders)
    {
        // Pool extraction — must match the order in BuildRaiders
        // Guild pool indices: 0=TestBnetId, 1=GuildMaster, 2=NeedsChar, 3=SiteAdmin, 4=DeleteAccount, 5..35=guild-raider-01..31
        var guildPool = allRaiders.Take(5 + 31).ToList();
        var outsiderPool = allRaiders.Skip(5 + 31).ToList();

        var localTestIds = new HashSet<string> { TestBnetId, NeedsCharacterBnetId };

        var defs = CreateRunDefinitions();
        var runs = new List<object>();

        foreach (var def in defs)
        {
            var pool = def.IsGuildPool ? guildPool : outsiderPool;
            var creatorDoc = pool.FirstOrDefault(r =>
            {
                var d = (Dictionary<string, object?>)r;
                return (string)d["battleNetId"]! == def.CreatorBnetId;
            });

            if (creatorDoc is null) continue;

            var creatorDict = (Dictionary<string, object?>)creatorDoc;
            var creatorBnetId = (string)creatorDict["battleNetId"]!;
            var creatorChars = (List<Dictionary<string, object?>>)creatorDict["characters"]!;
            var creatorGuildId = GetGuildId(creatorChars);
            var creatorGuildName = GetGuildName(creatorChars);

            var startTime = SeedNow.AddHours(def.StartHoursFromNow);
            var docCreatedAt = SeedNow.AddHours(-48);
            var expiryMs = startTime.AddDays(7);
            var ttl = (int)Math.Max(86400, (expiryMs - docCreatedAt).TotalSeconds);

            var signups = new List<Dictionary<string, object?>>();

            if (def.IncludeTestRaider && def.IsGuildPool)
            {
                var testRaider = guildPool[0]; // TEST_MODE_IDENTITY
                signups.Add(BuildSignup(def.Id, testRaider, 0));
            }

            var availablePool = pool
                .Where(r =>
                {
                    var d = (Dictionary<string, object?>)r;
                    return !localTestIds.Contains((string)d["battleNetId"]!);
                })
                .ToList();

            var remaining = Math.Max(0, def.SignupCount - signups.Count);
            var selected = SelectRaiders(availablePool, remaining, def.PoolOffset);
            var attendanceOffset = signups.Count;
            foreach (var (raider, idx) in selected.Select((r, i) => (r, i)))
            {
                signups.Add(BuildSignup(def.Id, raider, attendanceOffset + idx));
            }

            var run = new Dictionary<string, object?>
            {
                ["id"] = def.Id,
                ["startTime"] = startTime.ToString("O"),
                ["signupCloseTime"] = SeedNow.AddHours(def.SignupCloseHoursFromNow).ToString("O"),
                ["description"] = def.Description,
                ["modeKey"] = def.ModeKey,
                ["visibility"] = def.Visibility,
                ["creatorGuild"] = creatorGuildName ?? "",
                ["creatorGuildId"] = creatorGuildId,
                ["instanceId"] = def.InstanceId,
                ["instanceName"] = def.InstanceName,
                ["creatorBattleNetId"] = creatorBnetId,
                ["createdAt"] = docCreatedAt.ToString("O"),
                ["ttl"] = ttl,
                ["runCharacters"] = signups,
            };

            runs.Add(run);
        }

        return runs;
    }

    private static int? GetGuildId(List<Dictionary<string, object?>> characters)
    {
        if (characters.Count == 0) return null;
        var profile = characters[0].GetValueOrDefault("profileSummary") as Dictionary<string, object?>;
        var guild = profile?.GetValueOrDefault("guild") as Dictionary<string, object?>;
        if (guild is null) return null;
        return guild["id"] as int?;
    }

    private static string? GetGuildName(List<Dictionary<string, object?>> characters)
    {
        if (characters.Count == 0) return null;
        var profile = characters[0].GetValueOrDefault("profileSummary") as Dictionary<string, object?>;
        var guild = profile?.GetValueOrDefault("guild") as Dictionary<string, object?>;
        return guild?["name"] as string;
    }

    private static readonly string[] AttendanceCycle = ["IN", "IN", "IN", "BENCH", "LATE", "AWAY", "OUT"];

    private static readonly Dictionary<string, (string Name, string Role)> SpecData = new()
    {
        ["72"] = ("Fury", "DPS"),
        ["73"] = ("Protection", "TANK"),
        ["65"] = ("Holy", "HEALER"),
        ["66"] = ("Protection", "TANK"),
        ["70"] = ("Retribution", "DPS"),
        ["254"] = ("Marksmanship", "DPS"),
        ["255"] = ("Survival", "DPS"),
        ["256"] = ("Discipline", "HEALER"),
        ["258"] = ("Shadow", "DPS"),
        ["250"] = ("Blood", "TANK"),
        ["252"] = ("Unholy", "DPS"),
        ["262"] = ("Elemental", "DPS"),
        ["264"] = ("Restoration", "HEALER"),
        ["63"] = ("Fire", "DPS"),
        ["64"] = ("Frost", "DPS"),
        ["265"] = ("Affliction", "DPS"),
        ["267"] = ("Destruction", "DPS"),
        ["268"] = ("Brewmaster", "TANK"),
        ["270"] = ("Mistweaver", "HEALER"),
        ["102"] = ("Balance", "DPS"),
        ["104"] = ("Guardian", "TANK"),
        ["105"] = ("Restoration", "HEALER"),
    };

    private static Dictionary<string, object?> BuildSignup(string runId, object raiderObj, int index)
    {
        var raider = (Dictionary<string, object?>)raiderObj;
        var battleNetId = (string)raider["battleNetId"]!;
        var chars = (List<Dictionary<string, object?>>)raider["characters"]!;
        var primaryChar = chars.Count > 0 ? chars[0] : null;

        var charId = primaryChar?.GetValueOrDefault("id") as string ?? "";
        var charName = primaryChar?.GetValueOrDefault("name") as string ?? "";
        var charRealm = primaryChar?.GetValueOrDefault("realm") as string ?? "";
        var charLevel = primaryChar?.GetValueOrDefault("level") is int lvl ? lvl : 80;
        var charClassId = primaryChar?.GetValueOrDefault("classId") is int cid ? cid : 0;
        var charRaceId = primaryChar?.GetValueOrDefault("raceId") is int rid ? rid : 0;

        // Get class/race names from stored character template reference
        var (className, raceName) = GetCharClassRaceNames(charClassId, charRaceId);

        // Get active spec from specializationsSummary
        var specSummary = primaryChar?.GetValueOrDefault("specializationsSummary") as Dictionary<string, object?>;
        var activeSpec = specSummary?.GetValueOrDefault("active_specialization") as Dictionary<string, object?>;
        int? specId = activeSpec?.GetValueOrDefault("id") as int?;
        string? specName = activeSpec?.GetValueOrDefault("name") as string;
        string? role = specId.HasValue && SpecData.TryGetValue(specId.Value.ToString(), out var sd) ? sd.Role : null;

        return new Dictionary<string, object?>
        {
            ["id"] = $"{runId}-signup-{battleNetId}",
            ["characterId"] = charId,
            ["characterName"] = charName,
            ["characterRealm"] = charRealm,
            ["characterLevel"] = charLevel,
            ["characterClassId"] = charClassId,
            ["characterClassName"] = className,
            ["characterRaceId"] = charRaceId,
            ["characterRaceName"] = raceName,
            ["raiderBattleNetId"] = battleNetId,
            ["desiredAttendance"] = AttendanceCycle[index % AttendanceCycle.Length],
            ["reviewedAttendance"] = "IN",
            ["specId"] = specId,
            ["specName"] = specName,
            ["role"] = role,
        };
    }

    private static (string ClassName, string RaceName) GetCharClassRaceNames(int classId, int raceId)
    {
        var classNames = new Dictionary<int, string>
        {
            [1] = "Warrior",
            [2] = "Paladin",
            [3] = "Hunter",
            [4] = "Rogue",
            [5] = "Priest",
            [6] = "Death Knight",
            [7] = "Shaman",
            [8] = "Mage",
            [9] = "Warlock",
            [10] = "Monk",
            [11] = "Druid",
        };
        var raceNames = new Dictionary<int, string>
        {
            [1] = "Human",
            [2] = "Orc",
            [3] = "Dwarf",
            [4] = "Night Elf",
            [5] = "Undead",
            [6] = "Tauren",
            [7] = "Gnome",
            [8] = "Troll",
            [10] = "Blood Elf",
            [11] = "Draenei",
        };
        return (
            classNames.TryGetValue(classId, out var cn) ? cn : "Unknown",
            raceNames.TryGetValue(raceId, out var rn) ? rn : "Unknown");
    }

    private static List<object> SelectRaiders(List<object> pool, int count, int offset)
    {
        if (pool.Count == 0 || count <= 0) return [];
        var start = offset % pool.Count;
        var ordered = pool.Skip(start).Concat(pool.Take(start)).ToList();
        return ordered.Take(Math.Min(count, pool.Count)).ToList();
    }

    private static readonly Dictionary<int, string> InstanceNames = new()
    {
        [63] = "Deadmines",
        [249] = "Onyxia's Lair",
        [631] = "Icecrown Citadel",
        [741] = "Molten Core",
    };

    private static List<RunDef> CreateRunDefinitions()
    {
        var defs = new List<RunDef>
        {
            new("run-passed-public-deadmines", 63, "Deadmines", "HEROIC:5", "PUBLIC", TestBnetId,
                "Completed heroic speed run", -24, -30, 5, true),
            new("run-passed-guild-icc25", 631, "Icecrown Citadel", "HEROIC:25", "GUILD", "guild-raider-01",
                "Last week guild progression", -72, -78, 20, true),
            new("run-public-empty-deadmines", 63, "Deadmines", "NORMAL:5", "PUBLIC", TestBnetId,
                "Public dungeon warmup", 24, 18, 0, true),
            new("run-public-signup-target-icc25", 631, "Icecrown Citadel", "HEROIC:25", "PUBLIC", "guild-raider-01",
                "Heroic farm night", 48, 42, 14, true, PoolOffset: 2),
            new("run-public-existing-signup-onyxia25", 249, "Onyxia's Lair", "NORMAL:25", "PUBLIC", "guild-raider-02",
                "Dragon reset clear", 54, 46, 12, true, IncludeTestRaider: true, PoolOffset: 4),
            new("run-guild-sparse-icc10", 631, "Icecrown Citadel", "NORMAL:10", "GUILD", TestBnetId,
                "Guild ten-player alt run", 72, 64, 5, true, IncludeTestRaider: true, PoolOffset: 6),
            new("run-guild-dense-molten-core", 741, "Molten Core", "NORMAL:40", "GUILD", "guild-raider-03",
                "Guild retro forty-player night", 96, 88, 30, true, IncludeTestRaider: true, PoolOffset: 1),
            new("run-public-closed-deadmines", 63, "Deadmines", "HEROIC:5", "PUBLIC", "guild-raider-04",
                "Closed heroic cleanup", 8, -2, 4, true, PoolOffset: 8),
            new("run-guild-closed-icc10", 631, "Icecrown Citadel", "HEROIC:10", "GUILD", "guild-raider-05",
                "Closed progression lockout", 12, -3, 8, true, IncludeTestRaider: true, PoolOffset: 10),
            new("run-edit-closed-deadmines", 63, "Deadmines", "NORMAL:5", "PUBLIC", TestBnetId,
                "Edit closed test run", 6, -1, 3, true, PoolOffset: 12),
            new("run-outsider-guild-hidden", 631, "Icecrown Citadel", "NORMAL:25", "GUILD", "outsider-raider-01",
                "Rival guild only raid", 36, 30, 7, false),
        };

        // 12 public generated runs
        for (var i = 0; i < 12; i++)
        {
            var instanceId = new[] { 63, 249, 631, 741 }[i % 4];
            var modeKey = new[] { "NORMAL:5", "NORMAL:25", "HEROIC:10", "NORMAL:40" }[i % 4];
            var creatorBnetId = $"guild-raider-{((i % 10) + 6):D2}";
            var signupCount = new[] { 0, 3, 6, 12, 5, 8 }[i % 6];
            defs.Add(new RunDef(
                $"run-public-generated-{(i + 1):D2}",
                instanceId,
                InstanceNames[instanceId],
                modeKey,
                "PUBLIC",
                creatorBnetId,
                $"Public roster check {i + 1}",
                120 + i * 6,
                112 + i * 6,
                signupCount,
                true,
                IncludeTestRaider: i % 3 == 0,
                PoolOffset: i));
        }

        // 12 guild generated runs
        for (var i = 0; i < 12; i++)
        {
            var instanceId = new[] { 631, 63, 741, 249 }[i % 4];
            var modeKey = new[] { "NORMAL:10", "HEROIC:5", "NORMAL:40", "NORMAL:25" }[i % 4];
            var creatorBnetId = i % 4 == 0 ? TestBnetId : $"guild-raider-{((i % 12) + 10):D2}";
            var signupCount = new[] { 2, 4, 8, 10, 16, 20 }[i % 6];
            defs.Add(new RunDef(
                $"run-guild-generated-{(i + 1):D2}",
                instanceId,
                InstanceNames[instanceId],
                modeKey,
                "GUILD",
                creatorBnetId,
                $"Guild calendar slot {i + 1}",
                216 + i * 6,
                206 + i * 6 - (i % 3 == 0 ? 12 : 0),
                signupCount,
                true,
                IncludeTestRaider: i % 2 == 0,
                PoolOffset: i + 3));
        }

        // 8 outsider generated runs
        for (var i = 0; i < 8; i++)
        {
            var instanceId = new[] { 63, 249, 631, 741 }[i % 4];
            var modeKey = new[] { "NORMAL:5", "NORMAL:25", "HEROIC:25", "NORMAL:40" }[i % 4];
            var signupCount = new[] { 3, 5, 9, 14 }[i % 4];
            defs.Add(new RunDef(
                $"run-outsider-generated-{(i + 1):D2}",
                instanceId,
                InstanceNames[instanceId],
                modeKey,
                "GUILD",
                $"outsider-raider-{((i % 8) + 1):D2}",
                $"Rival guild event {i + 1}",
                168 + i * 5,
                158 + i * 5,
                signupCount,
                false,
                PoolOffset: i));
        }

        return defs;
    }

    /// <summary>
    /// Retry wrapper for Cosmos operations that may fail during emulator warm-up.
    /// Catches 408 (RequestTimeout), 503 (ServiceUnavailable), and TaskCanceledException.
    /// </summary>
    private static async Task<T> RetryCosmosAsync<T>(Func<Task<T>> operation, int maxAttempts = 5)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransientCosmosError(ex))
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 5));
            }
        }
    }

    private static async Task RetryCosmosAsync(Func<Task> operation, int maxAttempts = 5)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransientCosmosError(ex))
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 5));
            }
        }
    }

    private static bool IsTransientCosmosError(Exception ex) =>
        ex is CosmosException ce && ce.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.ServiceUnavailable
        || ex is TaskCanceledException
        || ex is IOException;
}
