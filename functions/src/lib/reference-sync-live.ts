import { createReferenceSyncPlan } from "./reference-sync.js";
import type { EntitySyncMeta } from "../types/index.js";

export interface ReferenceSyncDefinition<TIndexResponse> {
  name: string;
  entity: string;
  maxAgeMs: number;
  fetchIndex: (token: string) => Promise<TIndexResponse>;
  getDetails: (response: TIndexResponse) => Array<{
    id: number;
    href: string;
  }>;
}

export interface ReferenceSyncIo {
  readBlob: <T>(blobName: string) => Promise<T | null>;
  writeBlob: (blobName: string, data: unknown) => Promise<void>;
  fetchToken: () => Promise<string>;
  fetchJson: (pathOrHref: string, token: string) => Promise<unknown>;
  sleep?: (ms: number) => Promise<void>;
  now?: () => Date;
  log?: (message: string) => void;
}

export interface ReferenceSyncOptions {
  force?: boolean;
  delayMs?: number;
}

const DEFAULT_REQUEST_DELAY_MS = 100;

export async function syncReferenceEntities(
  definitions: ReferenceSyncDefinition<any>[],
  io: ReferenceSyncIo,
  options: ReferenceSyncOptions = {}
): Promise<{ results: Array<{ name: string; status: string }> }> {
  const results: Array<{ name: string; status: string }> = [];
  const now = io.now?.() ?? new Date();
  const sleep = io.sleep ?? (async () => undefined);
  let token: string | null = null;

  for (const definition of definitions) {
    const metaBlobName = `reference/${definition.entity}/meta.json`;
    const previousMeta = await io.readBlob<EntitySyncMeta>(metaBlobName);

    try {
      if (!options.force && previousMeta?.lastSuccessTime) {
        const ageMs = now.getTime() - new Date(previousMeta.lastSuccessTime).getTime();
        if (ageMs < definition.maxAgeMs) {
          results.push({ name: definition.name, status: "skipped (fresh)" });
          continue;
        }
      }

      token ??= await io.fetchToken();

      const indexResponse = await definition.fetchIndex(token);
      const plan = createReferenceSyncPlan({
        entity: definition.entity,
        indexResponse,
        getDetails: definition.getDetails,
      });

      await io.writeBlob(plan.indexBlobName, indexResponse);
      for (const detail of plan.details) {
        const response = await io.fetchJson(detail.href, token);
        await io.writeBlob(detail.blobName, response);
        await sleep(options.delayMs ?? DEFAULT_REQUEST_DELAY_MS);
      }

      await io.writeBlob(plan.metaBlobName, {
        lastSuccessTime: now.toISOString(),
        lastFailureTime: previousMeta?.lastFailureTime ?? null,
        lastFailureReason: previousMeta?.lastFailureReason ?? null,
      } satisfies EntitySyncMeta);

      results.push({ name: definition.name, status: `synced (${plan.documentCount} docs)` });
    } catch (error: unknown) {
      const reason = error instanceof Error ? error.message : String(error);
      io.log?.(`Failed to sync ${definition.name}: ${reason}`);
      await io.writeBlob(metaBlobName, {
        lastSuccessTime: previousMeta?.lastSuccessTime ?? null,
        lastFailureTime: now.toISOString(),
        lastFailureReason: reason,
      } satisfies EntitySyncMeta);
      results.push({ name: definition.name, status: `failed: ${reason}` });
    }
  }

  return { results };
}
