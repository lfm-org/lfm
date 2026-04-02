/**
 * Migration 20260403-raids-to-runs
 *
 * Expand phase of the raids → runs rename:
 *
 * Part A — Copy raids container to runs container:
 *   Reads all documents from the `raids` container, renames the `raidCharacters`
 *   field to `runCharacters` on each document, and writes them to the `runs` container.
 *   Documents already present in `runs` (same id) are skipped — safe to re-run.
 *   The `raids` container is NOT deleted here; that is the contract phase done later.
 *
 * Part B — Rename guild rankPermissions fields:
 *   On every guild document in the `guilds` container, renames inside each
 *   rankPermissions entry:
 *     canCreateGuildRaids → canCreateGuildRuns
 *     canSignupGuildRaids → canSignupGuildRuns
 *     canDeleteGuildRaids → canDeleteGuildRuns
 *   Guilds with no rankPermissions, or whose entries already use the new names,
 *   are skipped — safe to re-run.
 *
 * down() is a no-op — reverting a container copy/rename is not worth the risk.
 */
import { CosmosClient } from "@azure/cosmos";

// DB_NAME is read inside up() — not at module level — so that Vitest can set the env var
// before up() is called without running into ESM import-hoisting issues.

export async function up(client: CosmosClient): Promise<void> {
  const DB_NAME = process.env.COSMOS_DATABASE!;

  // ── Part A: Copy raids → runs, rename raidCharacters → runCharacters ──────

  console.log("[20260403-raids-to-runs] Part A: copying raids container to runs container");

  const raidsContainer = client.database(DB_NAME).container("raids");
  const runsContainer = client.database(DB_NAME).container("runs");

  const { resources: raids } = await raidsContainer.items
    .query({ query: "SELECT * FROM c" })
    .fetchAll();

  console.log(`[20260403-raids-to-runs] Found ${raids.length} documents in raids container`);

  // Fetch existing run ids to make the copy idempotent
  const { resources: existingRuns } = await runsContainer.items
    .query({ query: "SELECT c.id FROM c" })
    .fetchAll();
  const existingRunIds = new Set(existingRuns.map((r: { id: string }) => r.id));
  console.log(`[20260403-raids-to-runs] Runs container already has ${existingRunIds.size} document(s) — those will be skipped`);

  let copied = 0;
  let skipped = 0;

  for (const doc of raids) {
    if (existingRunIds.has(doc.id)) {
      skipped++;
      continue;
    }

    // Rename raidCharacters → runCharacters (if the old field exists)
    if ("raidCharacters" in doc && !("runCharacters" in doc)) {
      doc.runCharacters = doc.raidCharacters;
      delete doc.raidCharacters;
    }

    await runsContainer.items.upsert(doc);
    console.log(`[20260403-raids-to-runs] COPIED run ${doc.id}`);
    copied++;
  }

  console.log(`[20260403-raids-to-runs] Part A done. Copied: ${copied}, Skipped (already in runs): ${skipped}`);

  // ── Part B: Rename guild rankPermissions fields ───────────────────────────

  console.log("[20260403-raids-to-runs] Part B: updating guild rankPermissions field names");

  const guildsContainer = client.database(DB_NAME).container("guilds");

  const { resources: guilds } = await guildsContainer.items
    .query({ query: "SELECT * FROM c" })
    .fetchAll();

  console.log(`[20260403-raids-to-runs] Found ${guilds.length} guild document(s)`);

  let guildsUpdated = 0;
  let guildsSkipped = 0;

  for (const guild of guilds) {
    if (!Array.isArray(guild.rankPermissions) || guild.rankPermissions.length === 0) {
      guildsSkipped++;
      continue;
    }

    let changed = false;

    for (const rank of guild.rankPermissions) {
      if ("canCreateGuildRaids" in rank) {
        rank.canCreateGuildRuns = rank.canCreateGuildRaids;
        delete rank.canCreateGuildRaids;
        changed = true;
      }
      if ("canSignupGuildRaids" in rank) {
        rank.canSignupGuildRuns = rank.canSignupGuildRaids;
        delete rank.canSignupGuildRaids;
        changed = true;
      }
      if ("canDeleteGuildRaids" in rank) {
        rank.canDeleteGuildRuns = rank.canDeleteGuildRaids;
        delete rank.canDeleteGuildRaids;
        changed = true;
      }
    }

    if (!changed) {
      guildsSkipped++;
      continue;
    }

    await guildsContainer.item(guild.id, guild.id).replace(guild);
    console.log(`[20260403-raids-to-runs] UPDATED guild ${guild.id} rankPermissions`);
    guildsUpdated++;
  }

  console.log(`[20260403-raids-to-runs] Part B done. Updated: ${guildsUpdated}, Skipped: ${guildsSkipped}`);
}

export async function down(_client: CosmosClient): Promise<void> {
  console.log("[20260403-raids-to-runs] down() is a no-op — container copy/field renames are not reversed automatically");
}
