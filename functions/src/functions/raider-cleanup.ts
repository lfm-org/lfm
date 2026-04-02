import { app, InvocationContext, Timer } from "@azure/functions";
import { getRaidersContainer, getRunsContainer } from "../lib/cosmos.js";
import { scrubRaiderFromRuns, deleteRaiderDocument } from "../lib/raider-cleanup.js";
import { auditLog } from "../lib/audit.js";

const NINETY_DAYS_MS = 90 * 24 * 60 * 60 * 1000;

async function handler(_timer: Timer, context: InvocationContext): Promise<void> {
  const cutoff = new Date(Date.now() - NINETY_DAYS_MS).toISOString();
  const raidersContainer = getRaidersContainer();
  const runsContainer = getRunsContainer();

  const { resources: staleRaiders } = await raidersContainer.items.query<{ id: string; battleNetId: string }>({
    query: "SELECT c.id, c.battleNetId FROM c WHERE c.lastSeenAt < @cutoff OR NOT IS_DEFINED(c.lastSeenAt)",
    parameters: [{ name: "@cutoff", value: cutoff }],
  }).fetchAll();

  let removed = 0;
  let errors = 0;

  for (const raider of staleRaiders) {
    try {
      await scrubRaiderFromRuns(raider.battleNetId, runsContainer);
      await deleteRaiderDocument(raider.battleNetId, raidersContainer);
      auditLog(context, { action: "account.expired", actorId: "system:raider-cleanup", targetId: raider.battleNetId, result: "success" });
      removed++;
    } catch (err) {
      errors++;
      auditLog(context, { action: "account.expired", actorId: "system:raider-cleanup", targetId: raider.battleNetId, result: "failure", detail: String(err) });
    }
  }

  context.log(`Raider cleanup: removed ${removed} inactive accounts${errors > 0 ? `, ${errors} errors` : ""}`);
}

app.timer("raider-cleanup", {
  schedule: "0 0 4 * * *",
  handler,
});
