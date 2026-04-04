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

export async function readWowClasses(): Promise<WowClass[] | null> {
  const index = await readBlob<BlizzardPlayableClassIndexResponse>("reference/playable-class/index.json");
  if (!index) return null;
  const details = await readDetailMap<BlizzardPlayableClassResponse>("playable-class", index.classes.map((entry) => entry.id));
  return toWowClassViews(index, details);
}

export async function readWowRaces(): Promise<WowRace[] | null> {
  const index = await readBlob<BlizzardPlayableRaceIndexResponse>("reference/playable-race/index.json");
  if (!index) return null;
  const details = await readDetailMap<BlizzardPlayableRaceResponse>("playable-race", index.races.map((entry) => entry.id));
  return toWowRaceViews(index, details);
}

export async function readWowSpecializations(): Promise<WowSpecialization[] | null> {
  const index = await readBlob<BlizzardPlayableSpecializationIndexResponse>("reference/playable-specialization/index.json");
  if (!index) return null;
  const ids = index.character_specializations.map((entry) => entry.id);
  const details = await readDetailMap<BlizzardPlayableSpecializationResponse>("playable-specialization", ids);
  const media = await readDetailMap<BlizzardMediaSummary>("playable-specialization-media", ids);
  return toWowSpecializationViews(index, details, media);
}

export async function readWowSpecializationMap(): Promise<Map<number, WowSpecialization>> {
  return new Map((await readWowSpecializations() ?? []).map((spec) => [spec.id, spec]));
}

export async function readWowInstances(): Promise<WowInstance[] | null> {
  const index = await readBlob<BlizzardJournalInstanceIndexResponse>("reference/journal-instance/index.json");
  if (!index) return null;
  const details = await readDetailMap<BlizzardJournalInstanceResponse>(
    "journal-instance",
    index.instances.map((entry) => entry.id)
  );
  return toWowInstanceViews(index, details);
}
