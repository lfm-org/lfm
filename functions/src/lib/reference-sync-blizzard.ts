import { readBlob, writeBlob } from "./blob.js";
import { syncReferenceEntities, type ReferenceSyncDefinition } from "./reference-sync-live.js";
import type {
  BlizzardJournalInstanceIndexResponse,
  BlizzardPlayableClassIndexResponse,
  BlizzardPlayableRaceIndexResponse,
  BlizzardPlayableSpecializationIndexResponse,
} from "../types/blizzard.js";

const BLIZZARD_REQUEST_DELAY_MS = 100;

const ENTITY_SYNC_DEFS: Array<
  ReferenceSyncDefinition<
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
    getDetails: (response) =>
      (response as BlizzardPlayableClassIndexResponse).classes.map((entry) => ({
        id: entry.id,
        href: entry.key.href,
      })),
  },
  {
    name: "races",
    entity: "playable-race",
    maxAgeMs: 30 * 24 * 60 * 60 * 1000,
    fetchIndex: fetchRaces,
    getDetails: (response) =>
      (response as BlizzardPlayableRaceIndexResponse).races.map((entry) => ({
        id: entry.id,
        href: entry.key.href,
      })),
  },
  {
    name: "specializations",
    entity: "playable-specialization",
    maxAgeMs: 30 * 24 * 60 * 60 * 1000,
    fetchIndex: fetchSpecializations,
    getDetails: (response) =>
      (response as BlizzardPlayableSpecializationIndexResponse).character_specializations.map((entry) => ({
        id: entry.id,
        href: entry.key.href,
      })),
  },
  {
    name: "instances",
    entity: "journal-instance",
    maxAgeMs: 7 * 24 * 60 * 60 * 1000,
    fetchIndex: fetchInstances,
    getDetails: (response) =>
      (response as BlizzardJournalInstanceIndexResponse).instances.map((entry) => ({
        id: entry.id,
        href: entry.key.href,
      })),
  },
];

export async function syncBlizzardReferenceData(options: {
  force?: boolean;
  log?: (message: string) => void;
} = {}) {
  return syncReferenceEntities(ENTITY_SYNC_DEFS, {
    readBlob,
    writeBlob,
    fetchToken: fetchBlizzardToken,
    fetchJson: fetchStaticJson,
    sleep,
    log: options.log,
  }, {
    force: options.force,
    delayMs: BLIZZARD_REQUEST_DELAY_MS,
  });
}

function blizzardApiBase(): string {
  return `https://${process.env.BATTLE_NET_REGION || "eu"}.api.blizzard.com`;
}

function staticNamespace(): string {
  const region = process.env.BATTLE_NET_REGION || "eu";
  return `static-${region}`;
}

function staticUrl(pathOrHref: string): string {
  if (pathOrHref.startsWith("https://")) return pathOrHref;

  const url = new URL(`${blizzardApiBase()}${pathOrHref}`);
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

async function fetchStaticJson(pathOrHref: string, token: string): Promise<unknown> {
  const response = await fetch(staticUrl(pathOrHref), {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!response.ok) throw new Error(`fetchStaticJson failed for ${pathOrHref}: ${response.status}`);
  return response.json() as Promise<unknown>;
}

async function fetchClasses(token: string): Promise<BlizzardPlayableClassIndexResponse> {
  return fetchStaticJson("/data/wow/playable-class/index", token) as Promise<BlizzardPlayableClassIndexResponse>;
}

async function fetchRaces(token: string): Promise<BlizzardPlayableRaceIndexResponse> {
  return fetchStaticJson("/data/wow/playable-race/index", token) as Promise<BlizzardPlayableRaceIndexResponse>;
}

async function fetchSpecializations(token: string): Promise<BlizzardPlayableSpecializationIndexResponse> {
  return fetchStaticJson("/data/wow/playable-specialization/index", token) as Promise<BlizzardPlayableSpecializationIndexResponse>;
}

async function fetchInstances(token: string): Promise<BlizzardJournalInstanceIndexResponse> {
  return fetchStaticJson("/data/wow/journal-instance/index", token) as Promise<BlizzardJournalInstanceIndexResponse>;
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
