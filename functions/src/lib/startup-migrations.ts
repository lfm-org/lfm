/**
 * Runs pending Cosmos DB migrations at Function App startup.
 *
 * Called once via top-level await in index.ts before any function handlers are registered.
 * Uses the Function App's managed identity (DefaultAzureCredential) — no key-based auth.
 *
 * Migrations are listed explicitly so the runtime does not need to read the filesystem.
 * To add a migration: import it below and append an entry to the migrations array.
 */
import { CosmosClient } from "@azure/cosmos";
import { DefaultAzureCredential } from "@azure/identity";
import { Umzug } from "umzug";
import { CosmosMigrationsStorage } from "./cosmos-migrations-storage.js";
import * as migration20260321 from "../scripts/migrations/20260321-raid-guild.js";
import * as migration20260322 from "../scripts/migrations/20260322-raid-guild-fallback.js";

export async function runStartupMigrations(): Promise<void> {
  const endpoint = process.env.COSMOS_ENDPOINT;
  const dbName = process.env.COSMOS_DATABASE;
  if (!endpoint || !dbName) {
    console.warn("[migrations] COSMOS_ENDPOINT or COSMOS_DATABASE not set — skipping startup migrations");
    return;
  }

  console.log("[migrations] Running startup migrations...");

  const client = new CosmosClient({ endpoint, aadCredentials: new DefaultAzureCredential() });
  const { container } = await client.database(dbName).containers.createIfNotExists({
    id: "migrations",
    partitionKey: { paths: ["/id"] },
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
