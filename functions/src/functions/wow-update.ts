import { app, HttpRequest, HttpResponseInit, InvocationContext, Timer } from "@azure/functions";
import { readBlob, writeBlob } from "../lib/blob.js";
import { createReferenceSyncPlan } from "../lib/reference-sync.js";
import { jsonResponse } from "../middleware/security-headers.js";
import type {
  BlizzardJournalInstanceIndexResponse,
  BlizzardPlayableClassIndexResponse,
  BlizzardPlayableRaceIndexResponse,
  BlizzardPlayableSpecializationIndexResponse,
} from "../types/blizzard.js";
import type { EntitySyncMeta } from "../types/index.js";

const BLIZZARD_REQUEST_DELAY_MS = 100;

interface ReferenceSyncDef<TIndexResponse> {
  name: string;
  entity: string;
  maxAgeMs: number;
  fetchIndex: (token: string) => Promise<TIndexResponse>;
  getDetailIds: (response: TIndexResponse) => number[];
  getDetailPath: (id: number) => string;
}

const ENTITY_SYNC_DEFS: Array<
  ReferenceSyncDef<
    | BlizzardPlayableClassIndexResponse
    | BlizzardPlayableRaceIndexResponse
    | BlizzardPlayableSpecializationIndexResponse
    | BlizzardJournalInstanceIndexResponse
  >
> = [
  {
    name: "classes",
    entity: "playable-class",
    maxAgeMs: 30 * 24 * 60 * 60 * 1000,
    fetchIndex: fetchClasses,
    getDetailIds: (response) => (response as BlizzardPlayableClassIndexResponse).classes.map((entry) => entry.id),
    getDetailPath: (id) => `/data/wow/playable-class/${id}`,
  },
  {
    name: "races",
    entity: "playable-race",
    maxAgeMs: 30 * 24 * 60 * 60 * 1000,
    fetchIndex: fetchRaces,
    getDetailIds: (response) => (response as BlizzardPlayableRaceIndexResponse).races.map((entry) => entry.id),
    getDetailPath: (id) => `/data/wow/playable-race/${id}`,
  },
  {
    name: "specializations",
    entity: "playable-specialization",
    maxAgeMs: 30 * 24 * 60 * 60 * 1000,
    fetchIndex: fetchSpecializations,
    getDetailIds: (response) => (response as BlizzardPlayableSpecializationIndexResponse).character_specializations.map((entry) => entry.id),
    getDetailPath: (id) => `/data/wow/playable-specialization/${id}`,
  },
  {
    name: "instances",
    entity: "journal-instance",
    maxAgeMs: 7 * 24 * 60 * 60 * 1000,
    fetchIndex: fetchInstances,
    getDetailIds: (response) => (response as BlizzardJournalInstanceIndexResponse).instances.map((entry) => entry.id),
    getDetailPath: (id) => `/data/wow/journal-instance/${id}`,
  },
];

export async function syncEntities(context: InvocationContext): Promise<{ results: Array<{ name: string; status: string }> }> {
  const token = await fetchBlizzardToken();
  const results: Array<{ name: string; status: string }> = [];

  for (const def of ENTITY_SYNC_DEFS) {
    const metaBlob = `reference/${def.entity}/meta.json`;

    try {
      const meta = await readBlob<EntitySyncMeta>(metaBlob);
      if (meta?.lastSuccessTime) {
        const age = Date.now() - new Date(meta.lastSuccessTime).getTime();
        if (age < def.maxAgeMs) {
          results.push({ name: def.name, status: "skipped (fresh)" });
          continue;
        }
      }

      const indexResponse = await def.fetchIndex(token);
      const plan = createReferenceSyncPlan({
        entity: def.entity,
        indexResponse,
        getDetailIds: def.getDetailIds,
        getDetailPath: def.getDetailPath,
      });

      await writeBlob(plan.indexBlobName, indexResponse);
      for (const detail of plan.details) {
        const response = await fetchStaticJson(detail.path, token);
        await writeBlob(detail.blobName, response);
        await sleep(BLIZZARD_REQUEST_DELAY_MS);
      }

      await writeBlob(plan.metaBlobName, {
        lastSuccessTime: new Date().toISOString(),
        lastFailureTime: meta?.lastFailureTime ?? null,
        lastFailureReason: meta?.lastFailureReason ?? null,
      } satisfies EntitySyncMeta);

      results.push({ name: def.name, status: `synced (${plan.documentCount} docs)` });
    } catch (error: unknown) {
      const reason = error instanceof Error ? error.message : String(error);
      context.log(`Failed to sync ${def.name}: ${reason}`);
      const existingMeta = await readBlob<EntitySyncMeta>(metaBlob);
      await writeBlob(metaBlob, {
        lastSuccessTime: existingMeta?.lastSuccessTime ?? null,
        lastFailureTime: new Date().toISOString(),
        lastFailureReason: reason,
      } satisfies EntitySyncMeta);
      results.push({ name: def.name, status: `failed: ${reason}` });
    }
  }

  return { results };
}

async function httpHandler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const result = await syncEntities(context);
  return jsonResponse(result);
}

app.http("wow-update", {
  methods: ["POST"],
  route: "wow/update",
  authLevel: "function",
  handler: httpHandler,
});

async function timerHandler(timer: Timer, context: InvocationContext): Promise<void> {
  await syncEntities(context);
}

app.timer("wow-update-timer", {
  schedule: "0 0 6 * * 1",
  handler: timerHandler,
});

function blizzardApiBase(): string {
  return `https://${process.env.BATTLE_NET_REGION || "eu"}.api.blizzard.com`;
}

function staticNamespace(): string {
  const region = process.env.BATTLE_NET_REGION || "eu";
  return `static-${region}`;
}

function staticUrl(path: string): string {
  const url = new URL(`${blizzardApiBase()}${path}`);
  url.searchParams.set("namespace", staticNamespace());
  url.searchParams.set("locale", "en_US");
  return url.toString();
}

async function fetchBlizzardToken(): Promise<string> {
  const clientId = process.env.SISU_RAIDCAL_CLIENT_ID!;
  const clientSecret = process.env.SISU_RAIDCAL_CLIENT_SECRET!;
  const region = process.env.BATTLE_NET_REGION || "eu";
  const host = region === "cn" ? "gateway.battlenet.com.cn" : `${region}.battle.net`;
  const response = await fetch(`https://${host}/oauth/token`, {
    method: "POST",
    headers: {
      "Content-Type": "application/x-www-form-urlencoded",
      Authorization: `Basic ${Buffer.from(`${clientId}:${clientSecret}`).toString("base64")}`,
    },
    body: "grant_type=client_credentials",
  });
  if (!response.ok) throw new Error(`Token exchange failed: ${response.status}`);
  const data = await response.json() as { access_token: string };
  return data.access_token;
}

async function fetchStaticJson<T>(path: string, token: string): Promise<T> {
  const response = await fetch(staticUrl(path), {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!response.ok) throw new Error(`fetchStaticJson failed for ${path}: ${response.status}`);
  return response.json() as Promise<T>;
}

async function fetchClasses(token: string): Promise<BlizzardPlayableClassIndexResponse> {
  return fetchStaticJson<BlizzardPlayableClassIndexResponse>("/data/wow/playable-class/index", token);
}

async function fetchRaces(token: string): Promise<BlizzardPlayableRaceIndexResponse> {
  return fetchStaticJson<BlizzardPlayableRaceIndexResponse>("/data/wow/playable-race/index", token);
}

async function fetchSpecializations(token: string): Promise<BlizzardPlayableSpecializationIndexResponse> {
  return fetchStaticJson<BlizzardPlayableSpecializationIndexResponse>("/data/wow/playable-specialization/index", token);
}

async function fetchInstances(token: string): Promise<BlizzardJournalInstanceIndexResponse> {
  return fetchStaticJson<BlizzardJournalInstanceIndexResponse>("/data/wow/journal-instance/index", token);
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
