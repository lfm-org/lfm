/**
 * One-off migration: remap old AttendanceStatus values to new values.
 * Run dry-run first: DRY_RUN=true npx tsx functions/src/scripts/migrate-attendance.ts
 * Then run for real:  npx tsx functions/src/scripts/migrate-attendance.ts
 * Requires: COSMOS_ENDPOINT and COSMOS_KEY env vars.
 */
import { CosmosClient } from "@azure/cosmos";

const ATTENDANCE_MAP: Record<string, string> = {
  YES: "IN",
  IF_ROOM: "BENCH",
  NO: "OUT",
};

async function migrate() {
  const dryRun = process.env.DRY_RUN === "true";
  if (dryRun) console.log("DRY RUN — no writes will be made");

  const client = new CosmosClient({
    endpoint: process.env.COSMOS_ENDPOINT!,
    key: process.env.COSMOS_KEY!,
  });

  const dbName = process.env.COSMOS_DATABASE ?? "sisu-raidcal";
  const container = client.database(dbName).container("raids");

  const { resources: raids } = await container.items
    .query("SELECT * FROM c")
    .fetchAll();

  console.log(`Found ${raids.length} raid documents`);
  let updated = 0;

  for (const raid of raids) {
    let dirty = false;
    const changes: string[] = [];
    for (const rc of raid.raidCharacters ?? []) {
      if (ATTENDANCE_MAP[rc.desiredAttendance]) {
        changes.push(`  ${rc.characterName}: desiredAttendance ${rc.desiredAttendance} → ${ATTENDANCE_MAP[rc.desiredAttendance]}`);
        rc.desiredAttendance = ATTENDANCE_MAP[rc.desiredAttendance];
        dirty = true;
      }
      if (ATTENDANCE_MAP[rc.reviewedAttendance]) {
        changes.push(`  ${rc.characterName}: reviewedAttendance ${rc.reviewedAttendance} → ${ATTENDANCE_MAP[rc.reviewedAttendance]}`);
        rc.reviewedAttendance = ATTENDANCE_MAP[rc.reviewedAttendance];
        dirty = true;
      }
    }
    if (dirty) {
      console.log(`Raid ${raid.id}:\n${changes.join("\n")}`);
      if (!dryRun) {
        await container.item(raid.id, raid.id).replace(raid);
      }
      updated++;
    }
  }

  console.log(`\n${dryRun ? "[DRY RUN] Would update" : "Updated"} ${updated}/${raids.length} documents.`);
}

migrate().catch(err => { console.error(err); process.exit(1); });
