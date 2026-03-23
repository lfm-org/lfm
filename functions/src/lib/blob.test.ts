import { beforeEach, describe, expect, it, vi } from "vitest";

const upload = vi.fn();
const createIfNotExists = vi.fn();
const deleteIfExists = vi.fn();
const getBlockBlobClient = vi.fn(() => ({ upload }));
const getContainerClient = vi.fn(() => ({
  createIfNotExists,
  deleteIfExists,
  getBlockBlobClient,
}));
const BlobServiceClientConstructor = vi.fn(function () {
  return { getContainerClient };
});
const fromConnectionString = vi.fn(() => ({
  getContainerClient,
}));
const DefaultAzureCredentialConstructor = vi.fn();

vi.mock("@azure/storage-blob", () => ({
  BlobServiceClient: Object.assign(BlobServiceClientConstructor, {
    fromConnectionString,
  }),
}));

vi.mock("@azure/identity", () => ({
  DefaultAzureCredential: DefaultAzureCredentialConstructor,
}));

describe("writeBlob", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
    process.env.BLOB_STORAGE_URL = "https://lfmstore.blob.core.windows.net";
    delete process.env.AzureWebJobsStorage;
  });

  it("creates the wow container before uploading a blob", async () => {
    const { writeBlob } = await import("./blob.js");

    await writeBlob("instances.json", [{ id: 63, name: "Deadmines" }]);

    expect(createIfNotExists).toHaveBeenCalledTimes(1);
    expect(getBlockBlobClient).toHaveBeenCalledWith("instances.json");
    expect(upload).toHaveBeenCalledTimes(1);
  });

  it("uses the connection string instead of AAD for local HTTP blob storage", async () => {
    process.env.BLOB_STORAGE_URL = "http://azurite:10000/devstoreaccount1";
    process.env.AzureWebJobsStorage = "UseDevelopmentStorage=true";

    const { writeBlob } = await import("./blob.js");

    await writeBlob("instances.json", [{ id: 63, name: "Deadmines" }]);

    expect(fromConnectionString).toHaveBeenCalledWith("UseDevelopmentStorage=true");
    expect(DefaultAzureCredentialConstructor).not.toHaveBeenCalled();
    expect(BlobServiceClientConstructor).not.toHaveBeenCalled();
  });

  it("can reset the wow container for schema cleanup", async () => {
    const { resetWowContainer } = await import("./blob.js");

    await resetWowContainer();

    expect(deleteIfExists).toHaveBeenCalledTimes(1);
  });
});
