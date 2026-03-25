import { CosmosClient } from "@azure/cosmos";
import { pathToFileURL } from "url";
import { resetWowContainer } from "../lib/blob.js";
import { createCosmosClientOptions } from "../lib/cosmos.js";

export const WOW_BLOB_CONTAINER_NAME = "wow";
export const RESET_CONTAINER_IDS = ["raiders", "raids", "guilds"] as const;

const DB_NAME = process.env.COSMOS_DATABASE ?? "lfm";

interface RaiderResetDocument {
  id: string;
  battleNetId: string;
}

interface RaidResetDocument {
  id: string;
}

function isNotFound(error: unknown): boolean {
  return (error as { code?: number; statusCode?: number }).code === 404
    || (error as { code?: number; statusCode?: number }).statusCode === 404;
}

async function resetRaiders(client: CosmosClient): Promise<number> {
  const container = client.database(DB_NAME).container("raiders");

  try {
    const { resources } = await container.items.query<RaiderResetDocument>(
      "SELECT c.id, c.battleNetId FROM c"
    ).fetchAll();

    for (const resource of resources) {
      await container.item(resource.id, resource.battleNetId).delete();
    }

    return resources.length;
  } catch (error: unknown) {
    if (isNotFound(error)) return 0;
    throw error;
  }
}

async function resetRaids(client: CosmosClient): Promise<number> {
  const container = client.database(DB_NAME).container("raids");

  try {
    const { resources } = await container.items.query<RaidResetDocument>(
      "SELECT c.id FROM c"
    ).fetchAll();

    for (const resource of resources) {
      await container.item(resource.id, resource.id).delete();
    }

    return resources.length;
  } catch (error: unknown) {
    if (isNotFound(error)) return 0;
    throw error;
  }
}

async function resetGuilds(client: CosmosClient): Promise<number> {
  const container = client.database(DB_NAME).container("guilds");

  try {
    const { resources } = await container.items.query<RaidResetDocument>(
      "SELECT c.id FROM c"
    ).fetchAll();

    for (const resource of resources) {
      await container.item(resource.id, resource.id).delete();
    }

    return resources.length;
  } catch (error: unknown) {
    if (isNotFound(error)) return 0;
    throw error;
  }
}

export async function resetStorage(): Promise<void> {
  const client = new CosmosClient(createCosmosClientOptions());
  const [raidersDeleted, raidsDeleted, guildsDeleted] = await Promise.all([
    resetRaiders(client),
    resetRaids(client),
    resetGuilds(client),
  ]);

  await resetWowContainer();

  console.log(`Reset ${WOW_BLOB_CONTAINER_NAME} blob container`);
  console.log(`Deleted ${raidersDeleted} raider documents`);
  console.log(`Deleted ${raidsDeleted} raid documents`);
  console.log(`Deleted ${guildsDeleted} guild documents`);
}

async function main() {
  await resetStorage();
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error);
    process.exit(1);
  });
}
