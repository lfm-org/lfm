/**
 * Migration 20260321-raid-guild
 *
 * Backfills creatorGuild and creatorGuildId on raids that have null guild data.
 * Applies only to raids created before this migration ran (cutoff captured at start).
 * Idempotent: query filters on creatorGuildId = null so patched raids are skipped on re-run.
 *
 * down() is a no-op — guild data cannot be reliably recovered for historical raids.
 */
import { CosmosClient } from "@azure/cosmos";
import type { RaiderDocument, RaidDocument } from "../../types/index.js";
import type { BlizzardCharacterProfileSummary } from "../../types/blizzard.js";

const DB_NAME = "sisu-raidcal";

export async function up(client: CosmosClient): Promise<void> {
  const cutoff = new Date().toISOString();
  console.log(`[20260321-raid-guild] Backfilling raid guild data. Cutoff: ${cutoff}`);

  const raidsContainer = client.database(DB_NAME).container("raids");
  const raidersContainer = client.database(DB_NAME).container("raiders");

  const { resources: raids } = await raidsContainer.items
    .query<RaidDocument>({
      query: "SELECT * FROM c WHERE (NOT IS_DEFINED(c.creatorGuildId) OR IS_NULL(c.creatorGuildId)) AND c.createdAt < @cutoff",
      parameters: [{ name: "@cutoff", value: cutoff }],
    })
    .fetchAll();

  console.log(`[20260321-raid-guild] Found ${raids.length} raids to process`);

  let updated = 0;
  let skipped = 0;

  for (const raid of raids) {
    const { resource: raider } = await raidersContainer
      .item(raid.creatorBattleNetId, raid.creatorBattleNetId)
      .read<RaiderDocument>();

    if (!raider) {
      console.log(`[20260321-raid-guild] SKIP raid ${raid.id} — raider ${raid.creatorBattleNetId} not found`);
      skipped++;
      continue;
    }

    const selectedChar = raider.characters.find((c) => c.id === raider.selectedCharacterId);
    const guild = (selectedChar?.profileSummary as BlizzardCharacterProfileSummary | undefined)?.guild;

    if (!guild?.id) {
      console.log(`[20260321-raid-guild] SKIP raid ${raid.id} — creator has no guild on selected character`);
      skipped++;
      continue;
    }

    raid.creatorGuildId = guild.id;
    raid.creatorGuild = guild.name ?? "";

    await raidsContainer.item(raid.id, raid.id).replace(raid);
    console.log(`[20260321-raid-guild] PATCHED raid ${raid.id} → guild ${guild.name} (${guild.id})`);
    updated++;
  }

  console.log(`[20260321-raid-guild] Done. Updated: ${updated}, Skipped: ${skipped}`);
}

export async function down(_client: CosmosClient): Promise<void> {
  console.log("[20260321-raid-guild] down() is a no-op for this migration");
}
