// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;

namespace Lfm.E2E.Seeds;

/// <summary>
/// Seeds the <c>wow</c> container in Azurite with enough static Blizzard
/// reference data for the Phase-1 blob-backed read paths
/// (<c>/api/wow/reference/instances</c> and
/// <c>/api/wow/reference/specializations</c>) to return meaningful rows during
/// E2E. Mirrors the on-disk layout defined in <c>docs/storage-architecture.md</c>
/// — fixtures are intentionally minimal but shaped like the verbatim Blizzard
/// responses the legacy TS ingester wrote (localized-object names included so
/// the <see cref="Lfm.Api.Serialization.LocalizedStringConverter"/> is exercised).
/// </summary>
public static class WowReferenceSeed
{
    public const string ContainerName = "wow";

    public static async Task SeedAsync(string blobConnectionString)
    {
        var service = new BlobServiceClient(blobConnectionString);
        var container = service.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync();

        // --- Journal instances ---
        // Seeds just the instance referenced by DefaultSeed's test run
        // (instanceId 67, "Liberation of Undermine", NORMAL:25). Uses the
        // localized-object name shape on purpose — exercises the converter.
        await UploadJsonAsync(container, "reference/journal-instance/67.json", new
        {
            id = 67,
            name = new
            {
                en_US = "Liberation of Undermine",
                de_DE = "Befreiung von Schattenmark",
            },
            expansion = new { name = "The War Within" },
            modes = new[]
            {
                new { mode = new { type = "NORMAL" }, players = 25 },
            },
        });

        // --- Playable specializations ---
        // Specs referenced by the raiders seeded in DefaultSeed: 62 Arcane,
        // 65 Holy, 71 Arms. Kept to three so the dropdown has meaningful rows
        // without pretending to be a full spec index.
        await UploadSpecAsync(container, specId: 62, name: "Arcane", classId: 8, roleType: "DAMAGE");
        await UploadSpecAsync(container, specId: 65, name: "Holy", classId: 5, roleType: "HEALER");
        await UploadSpecAsync(container, specId: 71, name: "Arms", classId: 1, roleType: "DAMAGE");
    }

    private static async Task UploadSpecAsync(
        BlobContainerClient container, int specId, string name, int classId, string roleType)
    {
        await UploadJsonAsync(container, $"reference/playable-specialization/{specId}.json", new
        {
            id = specId,
            // Localized-object shape verifies the converter path.
            name = new Dictionary<string, string> { ["en_US"] = name },
            playable_class = new { id = classId },
            role = new { type = roleType },
        });

        await UploadJsonAsync(container, $"reference/playable-specialization-media/{specId}.json", new
        {
            assets = new[]
            {
                new { key = "icon", value = $"https://render.worldofwarcraft.com/e2e/spec-{specId}.jpg" },
            },
        });
    }

    private static async Task UploadJsonAsync(BlobContainerClient container, string blobName, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            // Blizzard field convention — keep snake_case keys verbatim so the
            // reader (Newtonsoft + [JsonProperty]) picks them up exactly as in prod.
            PropertyNamingPolicy = null,
            WriteIndented = false,
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        var blob = container.GetBlobClient(blobName);
        using var ms = new MemoryStream(bytes);
        await blob.UploadAsync(ms, overwrite: true);
    }
}
