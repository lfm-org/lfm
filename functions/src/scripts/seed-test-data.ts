import { CosmosClient, Container, PartitionKeyKind } from "@azure/cosmos";
import { pathToFileURL } from "url";
import { createCosmosClientOptions } from "../lib/cosmos.js";
import {
  assertLocalSeedEnvironment,
  DEFAULT_TEST_DATA_TIMESTAMP,
  buildSeedData,
  loadReferenceDataBundle,
  resolveE2eScenario,
  resolveSnapshotDir,
  type E2eScenario,
} from "./e2e-test-data.js";
import type { RaiderDocument, RaidDocument } from "../types/index.js";

const DB_NAME = process.env.COSMOS_DATABASE ?? "lfm";

export const RAIDERS_CONTAINER_DEFINITION = {
  id: "raiders",
  partitionKey: {
    paths: ["/battleNetId"],
    kind: PartitionKeyKind.Hash,
  },
};

export const RAIDS_CONTAINER_DEFINITION = {
  id: "raids",
  partitionKey: {
    paths: ["/id"],
    kind: PartitionKeyKind.Hash,
  },
};

export function getRaidsContainerDefinitionForScenario(
  scenario: E2eScenario
): typeof RAIDS_CONTAINER_DEFINITION | null {
  return scenario === "raids-error" ? null : RAIDS_CONTAINER_DEFINITION;
}

async function resetContainer<T extends { id: string }>(
  container: Container,
  partitionKeyFor: (document: T) => string
) {
  const { resources } = await container.items.query<T>("SELECT * FROM c").fetchAll();
  for (const resource of resources) {
    await container.item(resource.id, partitionKeyFor(resource)).delete();
  }
}

async function upsertAll<T>(container: Container, documents: T[]) {
  for (const document of documents) {
    await container.items.upsert(document);
  }
}

async function main() {
  assertLocalSeedEnvironment();

  const snapshotDir = resolveSnapshotDir();
  const bundle = await loadReferenceDataBundle(snapshotDir);
  const seedTimestamp = process.env.TEST_DATA_BASE_TIME || DEFAULT_TEST_DATA_TIMESTAMP;
  const scenario = resolveE2eScenario(process.env.E2E_SCENARIO);
  const seed = buildSeedData({
    now: seedTimestamp,
    region: process.env.BATTLE_NET_REGION || "eu",
    instances: bundle.instances,
    scenario,
  });

  const client = new CosmosClient(createCosmosClientOptions());
  const { database } = await client.databases.createIfNotExists({ id: DB_NAME });
  const { container: raidersContainer } = await database.containers.createIfNotExists(RAIDERS_CONTAINER_DEFINITION);

  await resetContainer<RaiderDocument>(raidersContainer, (raider) => raider.battleNetId);
  await upsertAll(raidersContainer, seed.raiders);

  const raidsContainerDefinition = getRaidsContainerDefinitionForScenario(scenario);
  if (raidsContainerDefinition) {
    const { container: raidsContainer } = await database.containers.createIfNotExists(raidsContainerDefinition);
    await resetContainer<RaidDocument>(raidsContainer, (raid) => raid.id);
    await upsertAll(raidsContainer, seed.raids);
  }

  console.log(`Seeded ${seed.raiders.length} raiders and ${seed.raids.length} raids from ${snapshotDir}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error);
    process.exit(1);
  });
}
