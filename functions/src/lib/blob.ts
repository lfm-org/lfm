import { BlobServiceClient, ContainerClient } from "@azure/storage-blob";
import { DefaultAzureCredential } from "@azure/identity";

let _wowContainer: ContainerClient | null = null;

function createBlobServiceClient(
  env: Record<string, string | undefined> = process.env
): BlobServiceClient {
  const url = env.BLOB_STORAGE_URL;
  if (!url) throw new Error("BLOB_STORAGE_URL environment variable is not set");

  if (url.startsWith("http://")) {
    const connectionString = env.AzureWebJobsStorage;
    if (!connectionString) {
      throw new Error("AzureWebJobsStorage environment variable is required for local HTTP blob endpoints");
    }
    return BlobServiceClient.fromConnectionString(connectionString);
  }

  return new BlobServiceClient(url, new DefaultAzureCredential());
}

function getWowContainer(): ContainerClient {
  if (!_wowContainer) {
    _wowContainer = createBlobServiceClient().getContainerClient("wow");
  }
  return _wowContainer;
}

const blobUrl = (): string => process.env.BLOB_STORAGE_URL || "";

export async function readBlob<T>(blobName: string): Promise<T | null> {
  try {
    const container = getWowContainer();
    const blobClient = container.getBlobClient(blobName);
    const response = await blobClient.download();
    const body = await streamToString(response.readableStreamBody!);
    return JSON.parse(body) as T;
  } catch (error: unknown) {
    if ((error as { statusCode?: number }).statusCode === 404) return null;
    throw error;
  }
}

export async function writeBlob(blobName: string, data: unknown): Promise<void> {
  const container = getWowContainer();
  await container.createIfNotExists();
  const blockBlob = container.getBlockBlobClient(blobName);
  const content = JSON.stringify(data, null, 2);
  await blockBlob.upload(content, Buffer.byteLength(content), {
    blobHTTPHeaders: { blobContentType: "application/json" },
  });
}

export async function resetWowContainer(): Promise<void> {
  const container = getWowContainer();
  await container.deleteIfExists();
  _wowContainer = null;
}

export function getPublicBlobUrl(blobName: string): string {
  return `${blobUrl()}/wow/${blobName}`;
}

async function streamToString(stream: NodeJS.ReadableStream): Promise<string> {
  const chunks: Buffer[] = [];
  for await (const chunk of stream) {
    chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
  }
  return Buffer.concat(chunks).toString("utf-8");
}
