import { beforeEach, describe, expect, it, vi } from "vitest";
import { Readable } from "stream";

const upload = vi.fn();
const createIfNotExists = vi.fn();
const deleteIfExists = vi.fn();
const download = vi.fn(async () => ({
  readableStreamBody: Readable.from([Uint8Array.from([60, 62])]),
  contentType: "image/svg+xml",
}));
const getBlockBlobClient = vi.fn(() => ({ upload }));
const getBlobClient = vi.fn(() => ({ download }));
const getContainerClient = vi.fn(() => ({
  createIfNotExists,
  deleteIfExists,
  getBlockBlobClient,
  getBlobClient,
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

  it("can upload binary assets with an explicit content type", async () => {
    const { writeBinaryBlob } = await import("./blob.js");

    await writeBinaryBlob("guild-crests/12345/crest.svg", new Uint8Array([60, 62]), "image/svg+xml");

    expect(upload).toHaveBeenCalledWith(expect.any(Uint8Array), 2, {
      blobHTTPHeaders: { blobContentType: "image/svg+xml" },
    });
  });

  it("can read binary assets and preserve the stored content type", async () => {
    const { readBinaryBlob } = await import("./blob.js");

    const result = await readBinaryBlob("guild-crests/12345/crest.svg");

    expect(getBlobClient).toHaveBeenCalledWith("guild-crests/12345/crest.svg");
    expect(result).toEqual({
      bytes: new Uint8Array([60, 62]),
      contentType: "image/svg+xml",
    });
  });
});
