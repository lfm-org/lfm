import { app, HttpRequest, HttpResponseInit, InvocationContext, Timer } from "@azure/functions";
import { readBlob, writeBlob } from "../lib/blob.js";
import { jsonResponse, errorResponse } from "../middleware/security-headers.js";
import type { EntitySyncMeta, WowInstance, WowInstanceMode } from "../types/index.js";

interface EntitySyncDef {
  name: string;
  maxAgeMs: number;
  dataBlob: string;
  metaBlob: string;
  fetch: (token: string) => Promise<unknown[]>;
}

const ENTITY_SYNC_DEFS: EntitySyncDef[] = [
  {
    name: "classes",
    maxAgeMs: 30 * 24 * 60 * 60 * 1000,
    dataBlob: "classes.json",
    metaBlob: "classes-meta.json",
    fetch: fetchClasses,
  },
  {
    name: "races",
    maxAgeMs: 30 * 24 * 60 * 60 * 1000,
    dataBlob: "races.json",
    metaBlob: "races-meta.json",
    fetch: fetchRaces,
  },
  {
    name: "specializations",
    maxAgeMs: 30 * 24 * 60 * 60 * 1000,
    dataBlob: "specializations.json",
    metaBlob: "specializations-meta.json",
    fetch: fetchSpecializations,
  },
  {
    name: "instances",
    maxAgeMs: 7 * 24 * 60 * 60 * 1000,
    dataBlob: "instances.json",
    metaBlob: "instances-meta.json",
    fetch: fetchInstances,
  },
];

async function syncEntities(context: InvocationContext): Promise<{ results: Array<{ name: string; status: string }> }> {
  const token = await fetchBlizzardToken();
  const results: Array<{ name: string; status: string }> = [];

  for (const def of ENTITY_SYNC_DEFS) {
    try {
      const meta = await readBlob<EntitySyncMeta>(def.metaBlob);
      if (meta?.lastSuccessTime) {
        const age = Date.now() - new Date(meta.lastSuccessTime).getTime();
        if (age < def.maxAgeMs) {
          results.push({ name: def.name, status: "skipped (fresh)" });
          continue;
        }
      }

      const data = await def.fetch(token);
      await writeBlob(def.dataBlob, data);
      await writeBlob(def.metaBlob, {
        lastSuccessTime: new Date().toISOString(),
        lastFailureTime: meta?.lastFailureTime ?? null,
        lastFailureReason: meta?.lastFailureReason ?? null,
      } satisfies EntitySyncMeta);

      results.push({ name: def.name, status: `synced (${data.length} items)` });
      await new Promise(resolve => setTimeout(resolve, 20));
    } catch (error: unknown) {
      const reason = error instanceof Error ? error.message : String(error);
      context.log(`Failed to sync ${def.name}: ${reason}`);
      const existingMeta = await readBlob<EntitySyncMeta>(def.metaBlob);
      await writeBlob(def.metaBlob, {
        lastSuccessTime: existingMeta?.lastSuccessTime ?? null,
        lastFailureTime: new Date().toISOString(),
        lastFailureReason: reason,
      });
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

async function fetchClasses(token: string): Promise<unknown[]> {
  const region = process.env.BATTLE_NET_REGION || "eu";
  const response = await fetch(`${blizzardApiBase()}/data/wow/playable-class/index?namespace=static-${region}&locale=en_US`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!response.ok) throw new Error(`fetchClasses failed: ${response.status}`);
  const data = await response.json() as { classes: unknown[] };
  return data.classes;
}

async function fetchRaces(token: string): Promise<unknown[]> {
  const region = process.env.BATTLE_NET_REGION || "eu";
  const response = await fetch(`${blizzardApiBase()}/data/wow/playable-race/index?namespace=static-${region}&locale=en_US`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!response.ok) throw new Error(`fetchRaces failed: ${response.status}`);
  const data = await response.json() as { races: unknown[] };
  return data.races;
}

async function fetchSpecializations(token: string): Promise<unknown[]> {
  const region = process.env.BATTLE_NET_REGION || "eu";
  const response = await fetch(
    `${blizzardApiBase()}/data/wow/playable-specialization/index?namespace=static-${region}&locale=en_US`,
    { headers: { Authorization: `Bearer ${token}` } }
  );
  if (!response.ok) throw new Error(`fetchSpecializations failed: ${response.status}`);
  const data = await response.json() as {
    character_specializations: Array<{
      id: number;
      name: string;
      playable_class: { id: number; name: string };
      role: { type: "DAMAGE" | "HEALER" | "TANK" };
    }>;
  };
  return data.character_specializations.map(s => ({
    id: s.id,
    name: s.name,
    classId: s.playable_class.id,
    role: s.role.type === "DAMAGE" ? "DPS" : s.role.type,
  }));
}

interface BlizzardInstanceIndex {
  id: number;
  name: { en_US: string };
}

interface BlizzardInstanceDetail {
  id: number;
  name: string;
  category?: { type: string };
  expansion?: { id: number };
  minimum_level?: number;
  modes?: WowInstanceMode[];
}

function toWowInstanceMode(mode: WowInstanceMode): WowInstanceMode {
  return {
    mode: {
      type: mode.mode.type,
      name: mode.mode.name,
    },
    ...(mode.players !== undefined ? { players: mode.players } : {}),
    ...(mode.is_tracked !== undefined ? { is_tracked: mode.is_tracked } : {}),
  };
}

export function toWowInstance(detail: BlizzardInstanceDetail): WowInstance {
  return {
    id: detail.id,
    name: detail.name,
    type: detail.category?.type ?? "UNKNOWN",
    minLevel: detail.minimum_level ?? 0,
    expansionId: detail.expansion?.id ?? 0,
    modes: (detail.modes ?? []).map(toWowInstanceMode),
  };
}

async function fetchInstances(token: string): Promise<unknown[]> {
  const region = process.env.BATTLE_NET_REGION || "eu";
  const ns = `static-${region}`;
  const response = await fetch(`${blizzardApiBase()}/data/wow/journal-instance/index?namespace=${ns}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!response.ok) throw new Error(`fetchInstances failed: ${response.status}`);
  const data = await response.json() as { instances: BlizzardInstanceIndex[] };

  const enriched: unknown[] = [];
  for (const inst of data.instances) {
    const detail = await fetch(`${blizzardApiBase()}/data/wow/journal-instance/${inst.id}?namespace=${ns}&locale=en_US`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!detail.ok) continue;
    const d = await detail.json() as BlizzardInstanceDetail;
    enriched.push(toWowInstance(d));
    await new Promise(resolve => setTimeout(resolve, 20));
  }

  return enriched;
}
