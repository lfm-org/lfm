import type { Container } from "@azure/cosmos";
import type { RaidDocument } from "../types/index.js";

export function scrubRaidDocument(
  raid: RaidDocument,
  battleNetId: string
): { modified: boolean; raid: RaidDocument } {
  let modified = false;

  const raidCharacters = raid.raidCharacters.filter(rc => rc.raiderBattleNetId !== battleNetId);
  if (raidCharacters.length !== raid.raidCharacters.length) modified = true;

  const creatorBattleNetId = raid.creatorBattleNetId === battleNetId ? null : raid.creatorBattleNetId;
  if (creatorBattleNetId !== raid.creatorBattleNetId) modified = true;

  if (!modified) return { modified: false, raid };
  return {
    modified: true,
    raid: { ...raid, creatorBattleNetId, raidCharacters },
  };
}

export async function scrubRaiderFromRaids(battleNetId: string, raidsContainer: Container): Promise<number> {
  const { resources: raids } = await raidsContainer.items.query<RaidDocument>({
    query: `SELECT * FROM c WHERE c.creatorBattleNetId = @battleNetId OR ARRAY_CONTAINS(c.raidCharacters, {"raiderBattleNetId": @battleNetId}, true)`,
    parameters: [{ name: "@battleNetId", value: battleNetId }],
  }).fetchAll();

  let scrubbed = 0;
  await Promise.all(
    raids.map(async (raid) => {
      const result = scrubRaidDocument(raid, battleNetId);
      if (result.modified) {
        await raidsContainer.item(raid.id, raid.id).replace(result.raid);
        scrubbed++;
      }
    })
  );
  return scrubbed;
}

export async function deleteRaiderDocument(battleNetId: string, raidersContainer: Container): Promise<void> {
  await raidersContainer.item(battleNetId, battleNetId).delete();
}
