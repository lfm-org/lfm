/**
 * Migration 20260322-raids-utc
 *
 * Converts raid timestamps from implicit Finnish local time to UTC.
 * Prior to this migration, startTime and signupCloseTime were stored as
 * ISO strings without timezone offset (e.g. "2026-03-22T20:30:00"), which
 * implicitly represented Finnish local time (Europe/Helsinki, UTC+2/UTC+3).
 *
 * After this migration all timestamps end with "Z" (UTC).
 *
 * Idempotency: raids whose startTime already contains "Z" or "+" are skipped.
 *
 * down() is a no-op — original local-time strings cannot be recovered.
 */
import { CosmosClient } from "@azure/cosmos";
import { DateTime } from "luxon";

const SOURCE_ZONE = "Europe/Helsinki";
// DB_NAME is read inside up() — not at module level — so that Vitest can set the env var
// before up() is called without running into ESM import-hoisting issues.

function toUtc(iso: string): string {
  return DateTime.fromISO(iso, { zone: SOURCE_ZONE }).toUTC().toISO()!;
}

function alreadyUtc(iso: string): boolean {
  return iso.includes("Z") || iso.includes("+");
}

export async function up(client: CosmosClient): Promise<void> {
  const DB_NAME = process.env.COSMOS_DATABASE!;
  console.log("[20260322-raids-utc] Converting raid timestamps to UTC");

  const container = client.database(DB_NAME).container("raids");

  const { resources: raids } = await container.items
    .query({ query: "SELECT * FROM c" })
    .fetchAll();

  console.log(`[20260322-raids-utc] Found ${raids.length} raids`);

  let converted = 0;
  let skipped = 0;

  for (const raid of raids) {
    if (alreadyUtc(raid.startTime)) {
      skipped++;
      continue;
    }

    raid.startTime = toUtc(raid.startTime);

    if (raid.signupCloseTime) {
      raid.signupCloseTime = toUtc(raid.signupCloseTime);
    }

    await container.item(raid.id, raid.id).replace(raid);
    console.log(`[20260322-raids-utc] CONVERTED raid ${raid.id}: startTime → ${raid.startTime}`);
    converted++;
  }

  console.log(`[20260322-raids-utc] Done. Converted: ${converted}, Skipped: ${skipped}`);
}

export async function down(_client: CosmosClient): Promise<void> {
  console.log("[20260322-raids-utc] down() is a no-op — original local-time strings cannot be recovered");
}
