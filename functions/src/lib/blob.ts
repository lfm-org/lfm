import { BlobServiceClient, ContainerClient } from "@azure/storage-blob";
import { DefaultAzureCredential } from "@azure/identity";

let _wowContainer: ContainerClient | null = null;

export interface BinaryBlob {
  bytes: Uint8Array;
  contentType: string;
}

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

const blobUrl = (): string => process.env.PUBLIC_BLOB_STORAGE_URL || process.env.BLOB_STORAGE_URL || "";

export async function readBlob<T>(blobName: string): Promise<T | null> {
  const asset = await readBinaryBlob(blobName);
  if (!asset) return null;
  return JSON.parse(Buffer.from(asset.bytes).toString("utf-8")) as T;
}

export async function readBinaryBlob(blobName: string): Promise<BinaryBlob | null> {
  try {
    const container = getWowContainer();
    const blobClient = container.getBlobClient(blobName);
    const response = await blobClient.download();
    const body = await streamToBuffer(response.readableStreamBody!);
    return {
      bytes: new Uint8Array(body),
      contentType: response.contentType ?? "application/octet-stream",
    };
  } catch (error: unknown) {
    if ((error as { statusCode?: number }).statusCode === 404) return null;
    throw error;
  }
}

export async function writeBlob(blobName: string, data: unknown): Promise<void> {
  const content = JSON.stringify(data, null, 2);
  await writeBinaryBlob(blobName, new TextEncoder().encode(content), "application/json");
}

export async function writeBinaryBlob(blobName: string, bytes: Uint8Array, contentType: string): Promise<void> {
  const container = getWowContainer();
  await container.createIfNotExists();
  const blockBlob = container.getBlockBlobClient(blobName);
  await blockBlob.upload(bytes, bytes.byteLength, {
    blobHTTPHeaders: { blobContentType: contentType },
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
  return (await streamToBuffer(stream)).toString("utf-8");
}

async function streamToBuffer(stream: NodeJS.ReadableStream): Promise<Buffer> {
  const chunks: Buffer[] = [];
  for await (const chunk of stream) {
    chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
  }
  return Buffer.concat(chunks);
}
