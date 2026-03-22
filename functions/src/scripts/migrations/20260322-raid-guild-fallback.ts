/**
 * Migration 20260322-raid-guild-fallback
 *
 * Backfills creatorGuild and creatorGuildId for raids still missing guild data after
 * 20260321-raid-guild ran. That migration only read from profileSummary.guild, which
 * is only populated for raiders who authenticated after the fix was deployed. Raiders
 * who haven't re-logged yet still have null there but may have guild data in the legacy
 * accountGuildsSummary field.
 *
 * Guild resolution order:
 *  1. selectedChar.profileSummary.guild  (accurate, character-specific)
 *  2. raider.accountGuildsSummary.guilds[0].guild  (legacy fallback, account-wide)
 *
 * No cutoff filter — we want to catch any raid still missing guild data regardless of
 * when it was created. Idempotency is guaranteed by the IS_NULL(c.creatorGuildId) filter.
 *
 * down() is a no-op — guild data cannot be reliably recovered for historical raids.
 */
import { CosmosClient } from "@azure/cosmos";
import type { RaiderDocument, RaidDocument } from "../../types/index.js";
import type { BlizzardCharacterProfileSummary } from "../../types/blizzard.js";

const DB_NAME = process.env.COSMOS_DATABASE!;

export async function up(client: CosmosClient): Promise<void> {
  console.log("[20260322-raid-guild-fallback] Backfilling guild data for raids missed by 20260321 migration");

  const raidsContainer = client.database(DB_NAME).container("raids");
  const raidersContainer = client.database(DB_NAME).container("raiders");

  const { resources: raids } = await raidsContainer.items
    .query<RaidDocument>({
      query: "SELECT * FROM c WHERE NOT IS_DEFINED(c.creatorGuildId) OR IS_NULL(c.creatorGuildId)",
    })
    .fetchAll();

  console.log(`[20260322-raid-guild-fallback] Found ${raids.length} raids still missing guild data`);

  let updated = 0;
  let skipped = 0;

  for (const raid of raids) {
    const { resource: raider } = await raidersContainer
      .item(raid.creatorBattleNetId, raid.creatorBattleNetId)
      .read<RaiderDocument>();

    if (!raider) {
      console.log(`[20260322-raid-guild-fallback] SKIP raid ${raid.id} — raider ${raid.creatorBattleNetId} not found`);
      skipped++;
      continue;
    }

    const selectedChar = raider.characters.find((c) => c.id === raider.selectedCharacterId);

    // Prefer profileSummary.guild (character-specific), fall back to accountGuildsSummary (legacy)
    const profileGuild = (selectedChar?.profileSummary as BlizzardCharacterProfileSummary | undefined)?.guild;
    const legacyGuild = raider.accountGuildsSummary?.guilds?.[0]?.guild;
    const guild = profileGuild ?? legacyGuild;

    if (!guild?.id) {
      console.log(`[20260322-raid-guild-fallback] SKIP raid ${raid.id} — no guild data available for creator`);
      skipped++;
      continue;
    }

    const source = profileGuild ? "profileSummary" : "accountGuildsSummary";
    raid.creatorGuildId = guild.id;
    raid.creatorGuild = guild.name ?? "";

    await raidsContainer.item(raid.id, raid.id).replace(raid);
    console.log(`[20260322-raid-guild-fallback] PATCHED raid ${raid.id} → guild ${guild.name} (${guild.id}) [${source}]`);
    updated++;
  }

  console.log(`[20260322-raid-guild-fallback] Done. Updated: ${updated}, Skipped: ${skipped}`);
}

export async function down(_client: CosmosClient): Promise<void> {
  console.log("[20260322-raid-guild-fallback] down() is a no-op for this migration");
}
