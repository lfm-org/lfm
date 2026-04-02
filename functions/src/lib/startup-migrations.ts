/**
 * Runs pending Cosmos DB migrations at Function App startup.
 *
 * Called once via top-level await in index.ts before any function handlers are registered.
 * Uses the shared Cosmos client options helper so local HTTP/test environments can
 * use key auth while deployed environments continue to use AAD.
 *
 * Migrations are listed explicitly so the runtime does not need to read the filesystem.
 * To add a migration: import it below and append an entry to the migrations array.
 */
import { CosmosClient, PartitionKeyKind } from "@azure/cosmos";
import { Umzug } from "umzug";
import { createCosmosClientOptions } from "./cosmos.js";
import { CosmosMigrationsStorage } from "./cosmos-migrations-storage.js";
import * as migration20260321 from "../scripts/migrations/20260321-raid-guild.js";
import * as migration20260322 from "../scripts/migrations/20260322-raid-guild-fallback.js";
import * as migration20260403runs from "../scripts/migrations/20260403-raids-to-runs.js";
import * as migration20260403portraits from "../scripts/migrations/20260403-portrait-cdn-urls.js";

export async function runStartupMigrations(): Promise<void> {
  const endpoint = process.env.COSMOS_ENDPOINT;
  const dbName = process.env.COSMOS_DATABASE;
  if (!endpoint || !dbName) {
    console.warn("[migrations] COSMOS_ENDPOINT or COSMOS_DATABASE not set — skipping startup migrations");
    return;
  }

  if (process.env.TEST_MODE === "true" && process.env.E2E_SCENARIO === "runs-error") {
    console.log("[migrations] Skipping startup migrations for runs-error scenario.");
    return;
  }

  console.log("[migrations] Running startup migrations...");

  const client = new CosmosClient(createCosmosClientOptions());
  const { container } = await client.database(dbName).containers.createIfNotExists({
    id: "migrations",
    partitionKey: {
      paths: ["/id"],
      kind: PartitionKeyKind.Hash,
    },
  });

  const umzug = new Umzug({
    migrations: [
      {
        name: "20260321-raid-guild",
        up: async ({ context }: { context: CosmosClient }) => migration20260321.up(context),
        down: async ({ context }: { context: CosmosClient }) => migration20260321.down(context),
      },
      {
        name: "20260322-raid-guild-fallback",
        up: async ({ context }: { context: CosmosClient }) => migration20260322.up(context),
        down: async ({ context }: { context: CosmosClient }) => migration20260322.down(context),
      },
      {
        name: "20260403-raids-to-runs",
        up: async ({ context }: { context: CosmosClient }) => migration20260403runs.up(context),
        down: async ({ context }: { context: CosmosClient }) => migration20260403runs.down(context),
      },
      {
        name: "20260403-portrait-cdn-urls",
        up: async ({ context }: { context: CosmosClient }) => migration20260403portraits.up(context),
        down: async ({ context }: { context: CosmosClient }) => migration20260403portraits.down(context),
      },
    ],
    context: client,
    storage: new CosmosMigrationsStorage(container),
    logger: console,
  });

  const pending = await umzug.pending();
  if (pending.length === 0) {
    console.log("[migrations] No pending migrations.");
    return;
  }

  console.log(`[migrations] Pending: ${pending.map((m) => m.name).join(", ")}`);
  await umzug.up();
  console.log("[migrations] Startup migrations complete.");
}
