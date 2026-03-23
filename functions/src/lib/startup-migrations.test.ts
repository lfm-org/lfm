import { beforeEach, describe, expect, it, vi } from "vitest";

const createIfNotExists = vi.fn(async () => ({ container: { id: "migrations" } }));
const database = vi.fn(() => ({
  containers: {
    createIfNotExists,
  },
}));
const CosmosClientConstructor = vi.fn(function () {
  return {
    database,
  };
});
const pending = vi.fn(async () => []);
const up = vi.fn(async () => {});
const UmzugConstructor = vi.fn(function () {
  return {
    pending,
    up,
  };
});
const createCosmosClientOptions = vi.fn();

vi.mock("@azure/cosmos", () => ({
  CosmosClient: CosmosClientConstructor,
  PartitionKeyKind: {
    Hash: "Hash",
  },
}));

vi.mock("umzug", () => ({
  Umzug: UmzugConstructor,
}));

vi.mock("./cosmos.js", () => ({
  createCosmosClientOptions,
}));

vi.mock("./cosmos-migrations-storage.js", () => ({
  CosmosMigrationsStorage: vi.fn(),
}));

vi.mock("../scripts/migrations/20260321-raid-guild.js", () => ({
  up: vi.fn(async () => {}),
  down: vi.fn(async () => {}),
}));

vi.mock("../scripts/migrations/20260322-raid-guild-fallback.js", () => ({
  up: vi.fn(async () => {}),
  down: vi.fn(async () => {}),
}));

describe("runStartupMigrations", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
    process.env.COSMOS_ENDPOINT = "http://cosmosdb:8081";
    process.env.COSMOS_DATABASE = "lfm-e2e";
    process.env.COSMOS_KEY = "local-test-key";
    process.env.TEST_MODE = "true";
    delete process.env.E2E_SCENARIO;
    createCosmosClientOptions.mockReturnValue({
      endpoint: "http://cosmosdb:8081",
      key: "local-test-key",
    });
  });

  it("uses shared Cosmos client options for local HTTP startup migrations", async () => {
    const { runStartupMigrations } = await import("./startup-migrations.js");

    await runStartupMigrations();

    expect(createCosmosClientOptions).toHaveBeenCalledWith();
    expect(CosmosClientConstructor).toHaveBeenCalledWith({
      endpoint: "http://cosmosdb:8081",
      key: "local-test-key",
    });
    expect(createIfNotExists).toHaveBeenCalledWith({
      id: "migrations",
      partitionKey: {
        paths: ["/id"],
        kind: "Hash",
      },
    });
  });

  it("skips startup migrations for the raids-error e2e scenario", async () => {
    process.env.E2E_SCENARIO = "raids-error";
    const { runStartupMigrations } = await import("./startup-migrations.js");

    await runStartupMigrations();

    expect(createCosmosClientOptions).not.toHaveBeenCalled();
    expect(CosmosClientConstructor).not.toHaveBeenCalled();
    expect(createIfNotExists).not.toHaveBeenCalled();
  });
});
