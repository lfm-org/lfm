/**
 * Migration 20260405-raider-ttl
 *
 * Adds per-document TTL to all existing raider documents.
 *
 * Raiders with no TTL (or a TTL that differs from the target) are updated to
 * ttl = 180 * 86400 (180 days in seconds). Cosmos DB will then auto-delete
 * inactive raider documents 180 days after their last write — dead profiles
 * are pruned without manual cleanup.
 *
 * The migration is idempotent: documents already carrying the target TTL are
 * skipped, so re-running produces zero writes.
 *
 * down() is a no-op — removing TTL from documents is not worth the risk, and
 * the container has no container-level defaultTtl to restore.
 */
import { CosmosClient } from "@azure/cosmos";

const TARGET_TTL = 180 * 86400; // 180 days in seconds

export async function up(client: CosmosClient): Promise<void> {
  const DB_NAME = process.env.COSMOS_DATABASE!;

  console.log("[20260405-raider-ttl] Setting ttl on raider documents");

  const raidersContainer = client.database(DB_NAME).container("raiders");

  const { resources: raiders } = await raidersContainer.items
    .query({ query: "SELECT * FROM c" })
    .fetchAll();

  console.log(`[20260405-raider-ttl] Found ${raiders.length} raider document(s)`);

  let updated = 0;
  let skipped = 0;

  for (const doc of raiders) {
    if (doc.ttl === TARGET_TTL) {
      skipped++;
      continue;
    }

    await raidersContainer.item(doc.id, doc.id).replace({ ...doc, ttl: TARGET_TTL });
    console.log(`[20260405-raider-ttl] UPDATED raider ${doc.id}`);
    updated++;
  }

  console.log(`[20260405-raider-ttl] Done. Updated: ${updated}, Skipped (already at target TTL): ${skipped}`);
}

export async function down(_client: CosmosClient): Promise<void> {
  console.log("[20260405-raider-ttl] down() is a no-op — TTL removal is not automated");
}
