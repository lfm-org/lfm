import { readBlob } from "./blob.js";
import {
  toWowClassViews,
  toWowInstanceViews,
  toWowRaceViews,
  toWowSpecializationViews,
} from "./blizzard-adapters.js";
import type {
  BlizzardJournalInstanceIndexResponse,
  BlizzardJournalInstanceResponse,
  BlizzardMediaSummary,
  BlizzardPlayableClassIndexResponse,
  BlizzardPlayableClassResponse,
  BlizzardPlayableRaceIndexResponse,
  BlizzardPlayableRaceResponse,
  BlizzardPlayableSpecializationIndexResponse,
  BlizzardPlayableSpecializationResponse,
} from "../types/blizzard.js";
import type { WowClass, WowInstance, WowRace, WowSpecialization } from "../types/index.js";

async function readDetailMap<T>(entity: string, ids: number[]): Promise<Map<number, T>> {
  const entries = await Promise.all(ids.map(async (id): Promise<readonly [number, T] | null> => {
    const detail = await readBlob<T>(`reference/${entity}/${id}.json`);
    return detail ? [id, detail] as const : null;
  }));

  const filtered = entries.filter((entry): entry is readonly [number, T] => entry !== null);
  return new Map<number, T>(filtered);
}

// ---------------------------------------------------------------------------
// Blob fetch implementations (no caching)
// ---------------------------------------------------------------------------

async function loadWowClasses(): Promise<WowClass[] | null> {
  const index = await readBlob<BlizzardPlayableClassIndexResponse>("reference/playable-class/index.json");
  if (!index) return null;
  const details = await readDetailMap<BlizzardPlayableClassResponse>("playable-class", index.classes.map((entry) => entry.id));
  return toWowClassViews(index, details);
}

async function loadWowRaces(): Promise<WowRace[] | null> {
  const index = await readBlob<BlizzardPlayableRaceIndexResponse>("reference/playable-race/index.json");
  if (!index) return null;
  const details = await readDetailMap<BlizzardPlayableRaceResponse>("playable-race", index.races.map((entry) => entry.id));
  return toWowRaceViews(index, details);
}

async function loadWowSpecializations(): Promise<WowSpecialization[] | null> {
  const index = await readBlob<BlizzardPlayableSpecializationIndexResponse>("reference/playable-specialization/index.json");
  if (!index) return null;
  const ids = index.character_specializations.map((entry) => entry.id);
  const details = await readDetailMap<BlizzardPlayableSpecializationResponse>("playable-specialization", ids);
  const media = await readDetailMap<BlizzardMediaSummary>("playable-specialization-media", ids);
  return toWowSpecializationViews(index, details, media);
}

async function loadWowInstances(): Promise<WowInstance[] | null> {
  const index = await readBlob<BlizzardJournalInstanceIndexResponse>("reference/journal-instance/index.json");
  if (!index) return null;
  const details = await readDetailMap<BlizzardJournalInstanceResponse>(
    "journal-instance",
    index.instances.map((entry) => entry.id)
  );
  return toWowInstanceViews(index, details);
}

// ---------------------------------------------------------------------------
// Module-level memo state
// Reference data is static for the worker lifetime; Function redeploy
// replaces the worker, which is the authoritative refresh mechanism.
// ---------------------------------------------------------------------------

let classesCache: WowClass[] | null | undefined;
let classesPromise: Promise<WowClass[] | null> | undefined;

let racesCache: WowRace[] | null | undefined;
let racesPromise: Promise<WowRace[] | null> | undefined;

let specsCache: WowSpecialization[] | null | undefined;
let specsPromise: Promise<WowSpecialization[] | null> | undefined;

let instancesCache: WowInstance[] | null | undefined;
let instancesPromise: Promise<WowInstance[] | null> | undefined;

// ---------------------------------------------------------------------------
// Memoised public API (same names as before — all call sites unchanged)
// ---------------------------------------------------------------------------

export async function readWowClasses(): Promise<WowClass[] | null> {
  if (classesCache !== undefined) return classesCache;
  if (classesPromise) return classesPromise;
  classesPromise = loadWowClasses().then((d) => (classesCache = d));
  return classesPromise;
}

export async function readWowRaces(): Promise<WowRace[] | null> {
  if (racesCache !== undefined) return racesCache;
  if (racesPromise) return racesPromise;
  racesPromise = loadWowRaces().then((d) => (racesCache = d));
  return racesPromise;
}

export async function readWowSpecializations(): Promise<WowSpecialization[] | null> {
  if (specsCache !== undefined) return specsCache;
  if (specsPromise) return specsPromise;
  specsPromise = loadWowSpecializations().then((d) => (specsCache = d));
  return specsPromise;
}

export async function readWowSpecializationMap(): Promise<Map<number, WowSpecialization>> {
  return new Map((await readWowSpecializations() ?? []).map((spec) => [spec.id, spec]));
}

export async function readWowInstances(): Promise<WowInstance[] | null> {
  if (instancesCache !== undefined) return instancesCache;
  if (instancesPromise) return instancesPromise;
  instancesPromise = loadWowInstances().then((d) => (instancesCache = d));
  return instancesPromise;
}
