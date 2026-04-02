import type { Container } from "@azure/cosmos";
import type { RunDocument } from "../types/index.js";

export function scrubRunDocument(
  run: RunDocument,
  battleNetId: string
): { modified: boolean; run: RunDocument } {
  let modified = false;

  const runCharacters = run.runCharacters.filter(rc => rc.raiderBattleNetId !== battleNetId);
  if (runCharacters.length !== run.runCharacters.length) modified = true;

  const creatorBattleNetId = run.creatorBattleNetId === battleNetId ? null : run.creatorBattleNetId;
  if (creatorBattleNetId !== run.creatorBattleNetId) modified = true;

  if (!modified) return { modified: false, run };
  return {
    modified: true,
    run: { ...run, creatorBattleNetId, runCharacters },
  };
}

export async function scrubRaiderFromRuns(battleNetId: string, runsContainer: Container): Promise<number> {
  const { resources: runs } = await runsContainer.items.query<RunDocument>({
    query: `SELECT * FROM c WHERE c.creatorBattleNetId = @battleNetId OR ARRAY_CONTAINS(c.runCharacters, {"raiderBattleNetId": @battleNetId}, true)`,
    parameters: [{ name: "@battleNetId", value: battleNetId }],
  }).fetchAll();

  let scrubbed = 0;
  await Promise.all(
    runs.map(async (run) => {
      const result = scrubRunDocument(run, battleNetId);
      if (result.modified) {
        await runsContainer.item(run.id, run.id).replace(result.run);
        scrubbed++;
      }
    })
  );
  return scrubbed;
}

export async function deleteRaiderDocument(battleNetId: string, raidersContainer: Container): Promise<void> {
  await raidersContainer.item(battleNetId, battleNetId).delete();
}
