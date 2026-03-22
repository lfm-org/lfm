/**
 * Migration runner — applies pending Cosmos DB migrations using umzug.
 *
 * Usage:
 *   COSMOS_ENDPOINT=... npx tsx functions/src/scripts/run-migrations.ts
 *   DRY_RUN=true COSMOS_ENDPOINT=... npx tsx functions/src/scripts/run-migrations.ts
 *
 * Required env vars: COSMOS_ENDPOINT, COSMOS_DATABASE
 * Authentication: DefaultAzureCredential (managed identity / Azure CLI / workload identity)
 */
import { CosmosClient } from "@azure/cosmos";
import { DefaultAzureCredential } from "@azure/identity";
import { Umzug } from "umzug";
import { readdirSync } from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { CosmosMigrationsStorage } from "../lib/cosmos-migrations-storage.js";

const DRY_RUN = process.env.DRY_RUN === "true";

async function main() {
  const endpoint = process.env.COSMOS_ENDPOINT;
  const dbName = process.env.COSMOS_DATABASE;
  if (!endpoint || !dbName) {
    console.error("COSMOS_ENDPOINT and COSMOS_DATABASE must be set");
    process.exit(1);
  }

  if (DRY_RUN) console.log("DRY RUN — no writes will be made");

  const client = new CosmosClient({ endpoint, aadCredentials: new DefaultAzureCredential() });
  const { container } = await client.database(dbName).containers.createIfNotExists({
    id: "migrations",
    partitionKey: { paths: ["/id"] },
  });

  const scriptDir = path.dirname(fileURLToPath(import.meta.url));
  const migrationsDir = path.join(scriptDir, "migrations");

  // Use readdirSync for Node.js 18/20 compatibility (glob from fs/promises requires Node 22+)
  const files = readdirSync(migrationsDir)
    .filter((f) => f.endsWith(".ts"))
    .sort()
    .map((f) => path.join(migrationsDir, f));

  const umzug = new Umzug({
    migrations: files.map((file) => ({
      name: path.basename(file, ".ts"),
      up: async ({ context }: { context: CosmosClient }) => {
        if (DRY_RUN) {
          console.log(`[DRY RUN] Would run: ${path.basename(file, ".ts")}`);
          return;
        }
        const mod = await import(pathToFileURL(file).toString());
        await mod.up(context);
      },
      down: async ({ context }: { context: CosmosClient }) => {
        if (DRY_RUN) {
          console.log(`[DRY RUN] Would revert: ${path.basename(file, ".ts")}`);
          return;
        }
        const mod = await import(pathToFileURL(file).toString());
        await mod.down?.(context);
      },
    })),
    context: client,
    storage: DRY_RUN
      ? { logMigration: async () => {}, unlogMigration: async () => {}, executed: async () => [] }
      : new CosmosMigrationsStorage(container),
    logger: console,
  });

  const pending = await umzug.pending();
  if (pending.length === 0) {
    console.log("No pending migrations.");
    return;
  }

  console.log(`Pending migrations: ${pending.map((m) => m.name).join(", ")}`);
  await umzug.up();
  console.log("Migrations complete.");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
