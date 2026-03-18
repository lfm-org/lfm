import { BlobServiceClient, ContainerClient } from "@azure/storage-blob";

let _wowContainer: ContainerClient | null = null;

function getWowContainer(): ContainerClient {
  if (!_wowContainer) {
    const connStr = process.env.AzureWebJobsStorage;
    if (!connStr) throw new Error("AzureWebJobsStorage environment variable is not set");
    _wowContainer = BlobServiceClient.fromConnectionString(connStr).getContainerClient("wow");
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
