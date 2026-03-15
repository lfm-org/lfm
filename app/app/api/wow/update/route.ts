import { NextResponse } from "next/server";
import { prisma } from "@/lib/prisma";

// ── Types ──────────────────────────────────────────────────────────────────

interface WowAuth {
  access_token: string;
}

interface WowNamedRef {
  key: { href: string };
  name: { en_GB?: string };
  id: number;
}

interface WowIndexResponse<T> {
  [key: string]: T[];
}

// ── Blizzard API helpers ───────────────────────────────────────────────────

const BLIZZARD_REGION = (process.env.BATTLE_NET_REGION || "eu").toLowerCase();
const BASE_URL = `https://${BLIZZARD_REGION}.api.blizzard.com/data/wow`;
const RATE_LIMIT_DELAY_MS = 20;

async function getBlizzardToken(): Promise<string> {
  const credentials = Buffer.from(
    `${process.env.SISU_RAIDCAL_CLIENT_ID}:${process.env.SISU_RAIDCAL_CLIENT_SECRET}`
  ).toString("base64");

  const response = await fetch(
    `https://${BLIZZARD_REGION}.battle.net/oauth/token`,
    {
      method: "POST",
      headers: {
        Authorization: `Basic ${credentials}`,
        "Content-Type": "application/x-www-form-urlencoded",
      },
      body: "grant_type=client_credentials",
    }
  );
  if (!response.ok) {
    throw new Error(`Blizzard auth failed: ${response.status}`);
  }
  const data = (await response.json()) as WowAuth;
  return data.access_token;
}

function makeHeaders(
  token: string,
  namespace: string
): Record<string, string> {
  return {
    Authorization: `Bearer ${token}`,
    "Battlenet-Namespace": namespace,
  };
}

async function fetchJson<T>(url: string, headers: Record<string, string>): Promise<T> {
  const response = await fetch(url, { headers, signal: AbortSignal.timeout(10000) });
  if (!response.ok) throw new Error(`Fetch failed ${url}: ${response.status}`);
  return response.json() as Promise<T>;
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

// ── Data-driven entity sync definitions ───────────────────────────────────
//
// Each definition describes ONE Blizzard API resource type.  Adding a new
// resource type is a single entry here — no new functions required.
//
// Fields:
//   indexPath    – path appended to BASE_URL for the index endpoint
//   indexKey     – key in the index JSON response containing the array
//   namespace    – Battlenet-Namespace header value
//   upsert       – receives one detail-page JSON object and calls Prisma

interface EntitySyncDef {
  indexPath: string;
  indexKey: string;
  namespace: string;
  upsert: (token: string, headers: Record<string, string>, entry: WowNamedRef) => Promise<void>;
}

const ENTITY_SYNC_DEFS: EntitySyncDef[] = [
  {
    indexPath: "/playable-class/index",
    indexKey: "classes",
    namespace: `static-classic-${BLIZZARD_REGION}`,
    upsert: async (_token, headers, entry) => {
      await sleep(RATE_LIMIT_DELAY_MS);
      const detail = await fetchJson<{ id: number; name: { en_GB?: string } }>(
        entry.key.href,
        headers
      );
      await prisma.wowClass.upsert({
        where: { id: detail.id },
        update: { name: detail.name.en_GB || "" },
        create: { id: detail.id, name: detail.name.en_GB || "" },
      });
    },
  },
  {
    indexPath: "/playable-race/index",
    indexKey: "races",
    namespace: `static-classic-${BLIZZARD_REGION}`,
    upsert: async (_token, headers, entry) => {
      await sleep(RATE_LIMIT_DELAY_MS);
      const detail = await fetchJson<{
        id: number;
        name: { en_GB?: string };
        faction: { type: string };
      }>(entry.key.href, headers);
      await prisma.wowRace.upsert({
        where: { id: detail.id },
        update: { name: detail.name.en_GB || "", faction: detail.faction.type },
        create: {
          id: detail.id,
          name: detail.name.en_GB || "",
          faction: detail.faction.type,
        },
      });
    },
  },
  {
    indexPath: "/journal-instance/index",
    indexKey: "instances",
    namespace: `static-${BLIZZARD_REGION}`, // retail — classic has no instances API
    upsert: async (_token, headers, entry) => {
      await sleep(RATE_LIMIT_DELAY_MS);
      const detail = await fetchJson<{
        id: number;
        name: { en_GB?: string };
        category?: { type: string };
        minimum_level?: number;
        expansion?: { id: number };
        modes?: Array<{ mode: { name: { en_GB?: string } } }>;
      }>(entry.key.href, headers);
      const data = {
        name: detail.name.en_GB || "",
        type: detail.category?.type || "",
        minLevel: detail.minimum_level || 0,
        expansionId: detail.expansion?.id || 0,
        modes: detail.modes?.map((m) => m.mode.name.en_GB || "") ?? [],
      };
      await prisma.wowInstance.upsert({
        where: { id: detail.id },
        update: data,
        create: { id: detail.id, ...data },
      });
    },
  },
];

// ── Staleness check ────────────────────────────────────────────────────────

async function isUpdateNeeded(): Promise<boolean> {
  const oneMonthAgo = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000);
  const recentSuccess = await prisma.wowMeta.count({
    where: { createdTime: { gte: oneMonthAgo }, success: true },
  });
  return recentSuccess < 1;
}

// ── Route handler ──────────────────────────────────────────────────────────

export async function POST() {
  console.log("[wow/update] POST invoked");
  const { SISU_RAIDCAL_CLIENT_ID, SISU_RAIDCAL_CLIENT_SECRET } = process.env;
  if (!SISU_RAIDCAL_CLIENT_ID || !SISU_RAIDCAL_CLIENT_SECRET) {
    console.log("[wow/update] Blizzard credentials not configured — skipping");
    return NextResponse.json(
      { message: "Blizzard credentials not configured — skipping update" },
      { status: 200 }
    );
  }

  console.log("[wow/update] Checking staleness...");
  if (!(await isUpdateNeeded())) {
    console.log("[wow/update] Data is up to date — skipping");
    return NextResponse.json({ message: "WoW data is up to date" });
  }

  console.log("[wow/update] Update needed, fetching Blizzard token...");
  try {
    const token = await getBlizzardToken();
    console.log("[wow/update] Token acquired, syncing entities...");

    await Promise.all(
      ENTITY_SYNC_DEFS.map(async (def) => {
        console.log(`[wow/update] Syncing ${def.indexPath}...`);
        const headers = makeHeaders(token, def.namespace);
        const index = await fetchJson<WowIndexResponse<WowNamedRef>>(
          `${BASE_URL}${def.indexPath}`,
          headers
        );
        const entries = index[def.indexKey] ?? [];
        console.log(`[wow/update] ${def.indexPath}: ${entries.length} entries`);
        for (const entry of entries) {
          await def.upsert(token, headers, entry);
        }
        console.log(`[wow/update] ${def.indexPath}: done`);
      })
    );

    await prisma.wowMeta.create({ data: { success: true } });
    console.log("[wow/update] Sync complete ✔");
    return NextResponse.json({ message: "WoW data updated successfully" });
  } catch (error) {
    console.error("[wow/update] Failed:", error);
    await prisma.wowMeta.create({ data: { success: false } });
    return NextResponse.json(
      { error: "WoW data update failed" },
      { status: 500 }
    );
  }
}
